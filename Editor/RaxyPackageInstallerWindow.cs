using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace RAXY.PackageInstaller.Editor
{
    public sealed class RaxyPackageInstallerWindow : EditorWindow
    {
        private static readonly PackageDefinition[] Packages =
        {
            RaxyWithManualDependencies("com.raxy.animation", "RAXY Animation", "https://github.com/RobyRAX/RAXY-Animation.git", new[] { "com.raxy.utility", "com.raxy.core", "com.cysharp.unitask", "com.unity.addressables" }, new[] { "com.kybernetik.animancer" }),
            Raxy("com.raxy.core", "RAXY Core", "https://github.com/RobyRAX/RAXY-Core.git", "com.raxy.utility", "com.cysharp.unitask", "com.unity.addressables"),
            Raxy("com.raxy.dialogue", "RAXY Dialogue System", "https://github.com/RobyRAX/RAXY-Dialogue.git", "com.raxy.event", "com.raxy.ui", "com.raxy.utility", "com.raxy.utility.localization", "com.cysharp.unitask", "com.unity.addressables", "com.unity.timeline", "com.unity.ugui"),
            Raxy("com.raxy.event", "RAXY Event System", "https://github.com/RobyRAX/RAXY-Event.git"),
            Raxy("com.raxy.inputsystem", "RAXY Input System", "https://github.com/RobyRAX/RAXY-InputSystem.git", "com.raxy.event", "com.raxy.utility", "com.unity.inputsystem"),
            Raxy("com.raxy.interaction", "RAXY Interaction System", "https://github.com/RobyRAX/RAXY-Interaction.git"),
            Raxy("com.raxy.inventory", "RAXY Inventory System", "https://github.com/RobyRAX/RAXY-Inventory.git", "com.raxy.core", "com.raxy.utility.localization", "com.cysharp.unitask"),
            Raxy("com.raxy.loot", "RAXY Loot System", "https://github.com/RobyRAX/RAXY-Loot.git", "com.raxy.inventory", "com.raxy.utility", "com.raxy.core"),
            Raxy("com.raxy.movement", "RAXY Movement", "https://github.com/RobyRAX/RAXY-Movement.git", "com.raxy.utility", "com.cysharp.unitask"),
            Raxy("com.raxy.notification", "RAXY Notification System", "https://github.com/RobyRAX/RAXY-Notification.git", "com.raxy.core", "com.raxy.utility", "com.unity.ugui"),
            Raxy("com.raxy.pooling", "RAXY Pooling", "https://github.com/RobyRAX/RAXY-Pooling.git", "com.raxy.utility", "com.raxy.core", "com.unity.addressables", "com.unity.addressables.android"),
            Raxy("com.raxy.quest", "RAXY Quest System", "https://github.com/RobyRAX/RAXY-Quest.git", "com.raxy.inventory", "com.raxy.utility", "com.raxy.utility.localization", "com.cysharp.unitask"),
            Raxy("com.raxy.statemachine", "RAXY State Machine", "https://github.com/RobyRAX/RAXY-StateMachine.git", "com.cysharp.unitask"),
            Raxy("com.raxy.ui", "RAXY UI", "https://github.com/RobyRAX/RAXY-UI.git", "com.unity.ugui"),
            Raxy("com.raxy.utility", "RAXY Utility", "https://github.com/RobyRAX/RAXY-Utility.git", "com.unity.nuget.newtonsoft-json"),
            Raxy("com.raxy.utility.localization", "RAXY Localization", "https://github.com/RobyRAX/RAXY-Localization.git", "com.unity.localization", "com.cysharp.unitask", "com.unity.addressables"),
            Raxy("com.raxy.vfx", "RAXY VFX Manager", "https://github.com/RobyRAX/RAXY-Vfx.git", "com.raxy.core", "com.raxy.pooling", "com.raxy.utility", "com.unity.addressables"),

            External("com.cysharp.unitask", "UniTask", "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"),
            External("com.unity.addressables", "Addressables", "com.unity.addressables@3.1.0"),
            External("com.unity.addressables.android", "Addressables Android", "com.unity.addressables.android@1.1.0"),
            External("com.unity.inputsystem", "Input System", "com.unity.inputsystem@1.19.0"),
            External("com.unity.localization", "Localization", "com.unity.localization@1.5.12"),
            External("com.unity.nuget.newtonsoft-json", "Newtonsoft Json", "com.unity.nuget.newtonsoft-json@3.2.2"),
            External("com.unity.timeline", "Timeline", "com.unity.timeline@1.8.12"),
            External("com.unity.ugui", "Unity UI", "com.unity.ugui@2.0.0")
        };

        private static readonly Dictionary<string, PackageDefinition> PackageById = Packages.ToDictionary(package => package.Id);
        private static readonly Dictionary<string, string> ManualDependencyDisplayNames = new()
        {
            { "com.kybernetik.animancer", "Animancer" }
        };

        private readonly Queue<PackageDefinition> _installQueue = new();
        private readonly HashSet<string> _installedPackageIds = new();
        private readonly Dictionary<string, string> _packageStatuses = new();

        private ListRequest _listRequest;
        private AddRequest _addRequest;
        private PackageDefinition _currentPackage;
        private Vector2 _scroll;
        private string _lastStatus = "Ready.";

        private bool IsBusy => _listRequest != null || _addRequest != null || _installQueue.Count > 0;

        [MenuItem("Tools/RAXY/Package Installer")]
        public static void Open()
        {
            var window = GetWindow<RaxyPackageInstallerWindow>("RAXY Package Installer");
            window.minSize = new Vector2(720f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += TickRequests;
            RefreshInstalledPackages();
        }

        private void OnDisable()
        {
            EditorApplication.update -= TickRequests;
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawSummary();
            DrawPackageList();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(IsBusy))
                {
                    if (GUILayout.Button("Refresh Status", EditorStyles.toolbarButton, GUILayout.Width(110f)))
                        RefreshInstalledPackages();

                    if (GUILayout.Button("Install All", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                        StartInstall(Packages.Where(package => package.IsVisible).Select(package => package.Id));
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label(IsBusy ? "Package Manager busy" : "Idle", EditorStyles.miniLabel);
            }
        }

        private void DrawSummary()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("RAXY Packages", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(_lastStatus, MessageType.Info);
        }

        private void DrawPackageList()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var package in Packages.Where(package => package.IsVisible))
            {
                DrawPackageEntry(package);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawPackageEntry(PackageDefinition package)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField(package.DisplayName, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(package.Id, EditorStyles.miniLabel);
                    }

                    GUILayout.FlexibleSpace();

                    bool isInstalled = IsInstalled(package.Id);
                    string status = GetPackageStatus(package, isInstalled);

                    GUILayout.Label(status, GUILayout.Width(92f));

                    using (new EditorGUI.DisabledScope(IsBusy || isInstalled))
                    {
                        if (GUILayout.Button(isInstalled ? "Installed" : "Install", GUILayout.Width(82f)))
                            StartInstall(new[] { package.Id });
                    }
                }

                string dependencies = GetDependencySummary(package);
                EditorGUILayout.LabelField("Dependencies", dependencies);

                if (_packageStatuses.TryGetValue(package.Id, out string packageStatus) && !string.IsNullOrWhiteSpace(packageStatus))
                    EditorGUILayout.LabelField("Status", packageStatus);
            }
        }

        private string GetPackageStatus(PackageDefinition package, bool isInstalled)
        {
            if (_currentPackage != null && _currentPackage.Id == package.Id)
                return "Installing";

            if (_installQueue.Any(queuedPackage => queuedPackage.Id == package.Id))
                return "Queued";

            return isInstalled ? "Installed" : "Not Installed";
        }

        private string GetDependencySummary(PackageDefinition package)
        {
            if (package.DependencyIds.Length == 0)
                return package.ManualDependencyIds.Length == 0 ? "None" : string.Join(", ", package.ManualDependencyIds.Select(GetManualDependencySummary));

            var dependencyNames = package.DependencyIds.Select(GetPackageDisplayName)
                .Concat(package.ManualDependencyIds.Select(GetManualDependencySummary));

            return string.Join(", ", dependencyNames);
        }

        private static string GetPackageDisplayName(string packageId)
        {
            return PackageById.TryGetValue(packageId, out var package) ? package.DisplayName : packageId;
        }

        private static string GetManualDependencySummary(string packageId)
        {
            return $"{GetManualDependencyDisplayName(packageId)} (manual)";
        }

        private static string GetManualDependencyDisplayName(string packageId)
        {
            return ManualDependencyDisplayNames.TryGetValue(packageId, out string displayName) ? displayName : packageId;
        }

        private bool IsInstalled(string packageId)
        {
            return _installedPackageIds.Contains(packageId);
        }

        private void RefreshInstalledPackages()
        {
            if (IsBusy && _listRequest == null)
                return;

            _lastStatus = "Refreshing installed package status...";
            _listRequest = Client.List(true, true);
            Repaint();
        }

        private void StartInstall(IEnumerable<string> rootPackageIds)
        {
            if (IsBusy)
                return;

            if (!TryBuildInstallPlan(rootPackageIds, out var installPlan, out string error))
            {
                _lastStatus = error;
                Debug.LogError($"[RAXY Package Installer] {error}");
                Repaint();
                return;
            }

            if (installPlan.Count == 0)
            {
                _lastStatus = "All selected packages are already installed.";
                Repaint();
                return;
            }

            _installQueue.Clear();

            foreach (var package in installPlan)
            {
                _installQueue.Enqueue(package);
                _packageStatuses[package.Id] = "Queued.";
            }

            _lastStatus = $"Queued {installPlan.Count} package(s).";
            StartNextInstall();
        }

        private bool TryBuildInstallPlan(IEnumerable<string> rootPackageIds, out List<PackageDefinition> installPlan, out string error)
        {
            installPlan = new List<PackageDefinition>();
            error = null;

            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();

            foreach (string rootPackageId in rootPackageIds)
            {
                if (!ResolvePackage(rootPackageId, visited, visiting, installPlan, ref error))
                    return false;
            }

            installPlan = installPlan
                .Where(package => !IsInstalled(package.Id))
                .GroupBy(package => package.Id)
                .Select(group => group.First())
                .ToList();

            return true;
        }

        private bool ResolvePackage(
            string packageId,
            HashSet<string> visited,
            HashSet<string> visiting,
            List<PackageDefinition> installPlan,
            ref string error)
        {
            if (visited.Contains(packageId))
                return true;

            if (visiting.Contains(packageId))
            {
                error = $"Circular dependency detected at {packageId}.";
                return false;
            }

            if (!PackageById.TryGetValue(packageId, out var package))
            {
                error = $"Package `{packageId}` is not registered in the RAXY Package Installer catalog.";
                return false;
            }

            visiting.Add(packageId);

            foreach (string dependencyId in package.DependencyIds)
            {
                if (!ResolvePackage(dependencyId, visited, visiting, installPlan, ref error))
                    return false;
            }

            foreach (string manualDependencyId in package.ManualDependencyIds)
            {
                if (IsInstalled(manualDependencyId))
                    continue;

                error = $"{package.DisplayName} needs manual dependency {GetManualDependencyDisplayName(manualDependencyId)} (`{manualDependencyId}`) installed first.";
                return false;
            }

            visiting.Remove(packageId);
            visited.Add(packageId);

            if (!IsInstalled(package.Id))
                installPlan.Add(package);

            return true;
        }

        private void StartNextInstall()
        {
            _currentPackage = null;

            while (_installQueue.Count > 0)
            {
                var nextPackage = _installQueue.Dequeue();

                if (IsInstalled(nextPackage.Id))
                {
                    _packageStatuses[nextPackage.Id] = "Already installed. Skipped.";
                    continue;
                }

                _currentPackage = nextPackage;
                _packageStatuses[nextPackage.Id] = "Installing...";
                _lastStatus = $"Installing {nextPackage.DisplayName}...";
                _addRequest = Client.Add(nextPackage.InstallSource);

                Debug.Log($"[RAXY Package Installer] Installing {nextPackage.DisplayName} from {nextPackage.InstallSource}");
                Repaint();
                return;
            }

            _lastStatus = "Install complete.";
            Debug.Log("[RAXY Package Installer] Install complete.");
            RefreshInstalledPackages();
        }

        private void TickRequests()
        {
            if (_listRequest != null && _listRequest.IsCompleted)
            {
                CompleteListRequest();
            }

            if (_addRequest != null && _addRequest.IsCompleted)
            {
                CompleteAddRequest();
            }
        }

        private void CompleteListRequest()
        {
            if (_listRequest.Status == StatusCode.Success)
            {
                _installedPackageIds.Clear();

                foreach (var package in _listRequest.Result)
                {
                    if (!string.IsNullOrEmpty(package.name))
                        _installedPackageIds.Add(package.name);
                }

                _lastStatus = $"Found {_installedPackageIds.Count} installed package(s).";
            }
            else
            {
                _lastStatus = $"Failed to refresh package status: {_listRequest.Error.message}";
                Debug.LogError($"[RAXY Package Installer] {_lastStatus}");
            }

            _listRequest = null;
            Repaint();
        }

        private void CompleteAddRequest()
        {
            var completedPackage = _currentPackage;
            bool installSucceeded = _addRequest.Status == StatusCode.Success;

            if (installSucceeded)
            {
                if (completedPackage != null)
                {
                    _installedPackageIds.Add(completedPackage.Id);
                    _packageStatuses[completedPackage.Id] = "Installed.";
                    _lastStatus = $"Installed {completedPackage.DisplayName}.";
                    Debug.Log($"[RAXY Package Installer] Installed {completedPackage.DisplayName}.");
                }
            }
            else
            {
                string message = _addRequest.Error != null ? _addRequest.Error.message : "Unknown Package Manager error.";

                if (completedPackage != null)
                    _packageStatuses[completedPackage.Id] = $"Failed: {message}";

                _installQueue.Clear();
                _lastStatus = $"Install failed: {message}";
                Debug.LogError($"[RAXY Package Installer] Failed to install {completedPackage?.DisplayName ?? "package"}: {message}");
            }

            _addRequest = null;
            _currentPackage = null;

            if (installSucceeded)
                StartNextInstall();

            Repaint();
        }

        private static PackageDefinition Raxy(string id, string displayName, string gitUrl, params string[] dependencyIds)
        {
            return new PackageDefinition(id, displayName, gitUrl, true, dependencyIds, Array.Empty<string>());
        }

        private static PackageDefinition RaxyWithManualDependencies(string id, string displayName, string gitUrl, string[] dependencyIds, string[] manualDependencyIds)
        {
            return new PackageDefinition(id, displayName, gitUrl, true, dependencyIds, manualDependencyIds);
        }

        private static PackageDefinition External(string id, string displayName, string installSource, params string[] dependencyIds)
        {
            return new PackageDefinition(id, displayName, installSource, false, dependencyIds, Array.Empty<string>());
        }

        private sealed class PackageDefinition
        {
            public PackageDefinition(string id, string displayName, string installSource, bool isVisible, string[] dependencyIds, string[] manualDependencyIds)
            {
                Id = id;
                DisplayName = displayName;
                InstallSource = installSource;
                IsVisible = isVisible;
                DependencyIds = dependencyIds ?? Array.Empty<string>();
                ManualDependencyIds = manualDependencyIds ?? Array.Empty<string>();
            }

            public string Id { get; }
            public string DisplayName { get; }
            public string InstallSource { get; }
            public bool IsVisible { get; }
            public string[] DependencyIds { get; }
            public string[] ManualDependencyIds { get; }
        }
    }
}

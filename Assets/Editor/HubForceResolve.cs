
      using System.IO;
      using UnityEditor;
      using UnityEditor.PackageManager;

      [InitializeOnLoad]
      public static class HubForceResolve
      {
          private const string k_ScriptPath = "Assets\\Editor\\HubForceResolve.cs";
          private const string k_EditorFolderPath = "Assets\\Editor";
          private const string k_StateKey = "Hub.ForceResolve.State";
          private const string k_ManifestPath = "Packages/manifest.json";
          private const string k_ManifestContent = "{\n  \"dependencies\": {\n    \"com.unity.inputsystem\": \"6.7.0\",\n    \"com.unity.render-pipelines.universal\": \"17.7.0\",\n    \"com.unity.modules.localizationruntime\": \"1.0.0\"\n  }\n}\n";

          static HubForceResolve()
          {
              EditorApplication.delayCall += Tick;
          }

          static void Tick()
          {
              // Wait until the Editor is idle so the resolve/self-delete don't fight the import.
              if (EditorApplication.isCompiling || EditorApplication.isUpdating)
              {
                  EditorApplication.delayCall += Tick;
                  return;
              }

              if (SessionState.GetString(k_StateKey, "") == "")
              {
                  // Write the intended manifest inside the Editor process — this is *after*
                  // the Editor's own template-generation and UPM update-dependencies writes,
                  // so nothing can race with (and clobber) the value we put on disk. See
                  // HUB-7048. Guarded so a write failure (locked file, read-only perms, AV on
                  // Windows) still lets the resolve + self-delete run — the project degrades to
                  // the Editor-generated manifest instead of leaving a stray script behind.
                  try
                  {
                      File.WriteAllText(k_ManifestPath, k_ManifestContent);
                  }
                  catch (System.Exception e)
                  {
                      UnityEngine.Debug.LogWarning("[Unity Hub] Failed to restore captured Packages/manifest.json: " + e.Message);
                  }
                  // First idle pass: resolve. May reload, after which Tick runs again.
                  SessionState.SetString(k_StateKey, "resolved");
                  Client.Resolve();
                  EditorApplication.delayCall += Tick;
                  return;
              }

              // Resolved and idle again → remove the script.
              Cleanup();
          }

          static void Cleanup()
          {
              AssetDatabase.DeleteAsset(k_ScriptPath);
              CleanupEmptyEditorFolder();
          }

          static void CleanupEmptyEditorFolder()
          {
              string absolutePath = Path.GetFullPath(k_EditorFolderPath);

              if (!Directory.Exists(absolutePath)) return;

              string[] files = Directory.GetFiles(absolutePath);
              string[] dirs = Directory.GetDirectories(absolutePath);

              bool isEmpty = true;

              if (dirs.Length > 0)
              {
                  isEmpty = false;
              }

              foreach (string file in files)
              {
                  if (!file.EndsWith(".DS_Store"))
                  {
                      isEmpty = false;
                      break;
                  }
              }

              if (isEmpty)
              {
                  AssetDatabase.DeleteAsset(k_EditorFolderPath);
              }
          }
      }
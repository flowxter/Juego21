using System.IO;
using Blackjack.Client.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackjack.Client.Editor
{
    /// <summary>
    /// Crea la escena jugable la primera vez que se abre el proyecto.
    ///
    /// La escena se genera con el serializador de Unity en vez de venir escrita
    /// a mano en el repositorio: los .unity son YAML con identificadores
    /// internos y referencias por GUID, y escribirlos fuera del editor produce
    /// escenas que abren con los scripts en "Missing".
    ///
    /// Solo actúa si el fichero no existe, así que no pisa el trabajo de nadie.
    /// </summary>
    [InitializeOnLoad]
    public static class SceneBootstrap
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string ScenePath = ScenesFolder + "/Mesa.unity";

        static SceneBootstrap()
        {
            // Se aplaza: durante el arranque del editor el proyecto todavía se
            // está importando y crear cosas aquí mismo falla a medias.
            EditorApplication.delayCall += CreateSceneIfMissing;
        }

        [MenuItem("Blackjack/Regenerar escena de la mesa")]
        private static void Regenerate()
        {
            if (File.Exists(ScenePath))
            {
                bool replace = EditorUtility.DisplayDialog(
                    "Regenerar escena",
                    "Ya existe Assets/Scenes/Mesa.unity. ¿Sustituirla?",
                    "Sustituir", "Cancelar");

                if (!replace) return;
            }

            BuildScene();
        }

        private static void CreateSceneIfMissing()
        {
            if (File.Exists(ScenePath)) return;

            BuildScene();

            Debug.Log("Escena creada en " + ScenePath + ". Dale a Play para entrar en la mesa.");
        }

        private static void BuildScene()
        {
            if (!Directory.Exists(ScenesFolder))
            {
                Directory.CreateDirectory(ScenesFolder);
                AssetDatabase.Refresh();
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Cámara: la interfaz es IMGUI y se dibujaría igual sin ella, pero
            // sin cámara el juego avisa de que no hay nada renderizando y el
            // fondo queda en negro sucio.
            var cameraObject = new GameObject("Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            // Verde tapete, para que se parezca algo a una mesa desde ya.
            camera.backgroundColor = new Color(0.05f, 0.25f, 0.13f);
            camera.orthographic = true;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var clientObject = new GameObject("BlackjackClient");
            clientObject.AddComponent<GameRoot>();

            EditorSceneManager.SaveScene(scene, ScenePath);

            RegisterInBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Deja la escena como primera del build, para que un ejecutable
        /// arranque directamente en la mesa.
        /// </summary>
        private static void RegisterInBuildSettings()
        {
            EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;

            foreach (EditorBuildSettingsScene existing in current)
            {
                if (existing.path == ScenePath) return;
            }

            var updated = new EditorBuildSettingsScene[current.Length + 1];
            updated[0] = new EditorBuildSettingsScene(ScenePath, true);
            for (int i = 0; i < current.Length; i++) updated[i + 1] = current[i];

            EditorBuildSettings.scenes = updated;
        }
    }
}

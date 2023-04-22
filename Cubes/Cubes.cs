using OWML.ModHelper;
using UnityEngine;
using UnityEngine.InputSystem;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Runtime.Serialization.Formatters.Binary;

namespace Cubes
{
    public class Cubes : ModBehaviour
    {
        readonly float range = 5f;
        protected AudioSource audio;
        Texture2D[] blockTextures;
        Dictionary<string, List<AudioClip>> blockAudio = new();
        int block = 0;
        FirstPersonManipulator placer;
        bool vr = false;
        Dictionary<OWRigidbody, List<(Vector3Int, int, Transform)>> placedBlocks = new();
        Text text;
        float textTimer = 0f;

        private void Start()
        {
            LoadManager.OnCompleteSceneLoad += (scene, loadScene) =>
            {
                if (loadScene != OWScene.SolarSystem) return;
                audio = GameObject.Find("Player_Body/Audio_Player/OneShotAudio_Player").GetComponent<AudioSource>();
                string[] fileEntries = Directory.GetFiles(ModHelper.Manifest.ModFolderPath + "blocks");
                blockTextures = new Texture2D[fileEntries.Length];
                for (int i = 0; i < fileEntries.Length; i++)
                {
                    string path = fileEntries[i];
                    blockTextures[i] = GetTexture(path);
                    blockTextures[i].filterMode = FilterMode.Point;
                    blockTextures[i].name = GetFilename(path);
                }
                GameObject gravText = GameObject.Find("PlayerHUD/HelmetOnUI/UICanvas/SecondaryGroup/GForce/NumericalReadout/GravityText");
                text = Instantiate(gravText, gravText.transform).GetComponent<Text>();
                StartCoroutine(LateInitialize());
            };
        }

        private IEnumerator LateInitialize()
        {
            string[] fileEntries = Directory.GetFiles(ModHelper.Manifest.ModFolderPath + "blocks/audio");
            for (int i = 0; i < fileEntries.Length; i++)
            {
                string path = fileEntries[i];
                string name = Regex.Replace(GetFilename(path), "[0-9]", "");
                AudioClip clip = null;
                using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(path, UnityEngine.AudioType.OGGVORBIS))
                {
                    yield return www.SendWebRequest();

                    if (www.isNetworkError)
                    {
                        ModHelper.Console.WriteLine(www.error, OWML.Common.MessageType.Error);
                    }
                    else
                    {
                        clip = DownloadHandlerAudioClip.GetContent(www);
                        clip.name = name;
                        DontDestroyOnLoad(clip);
                    }
                }
                try
                {
                    blockAudio.Add(name, new List<AudioClip>() { clip });
                }
                catch
                {
                    blockAudio[name].Add(clip);
                }
            }
            text.text = "";
            text.transform.localPosition = new Vector3(-160, -120, 0);
            yield return new WaitForEndOfFrame();
            placer = FindObjectOfType<FirstPersonManipulator>();
            if (placer.gameObject != Locator.GetPlayerCamera().gameObject)
            {
                vr = true;
            }
        }

        private void Update()
        {
            if (OWInput.IsNewlyPressed(InputLibrary.lockOn) && !OWInput.IsInputMode(InputMode.Menu) && (!vr || placer._interactReceiver == null && placer._interactZone == null && OWInput.IsInputMode(InputMode.Character)))
            {
                if ((OWInput.IsPressed(InputLibrary.freeLook) || OWInput.IsPressed(InputLibrary.rollMode) && vr) && Physics.Raycast(placer.transform.position, placer.transform.forward, out RaycastHit hit, range, OWLayerMask.physicalMask | OWLayerMask.interactMask))
                {
                    GameObject target = hit.collider.gameObject;
                    if (target.name.Equals("cube"))
                    {
                        bool blockFound = false;
                        for (int i = 0; i < placedBlocks[hit.rigidbody.GetAttachedOWRigidbody()].Count; i++)
                        {
                            if (placedBlocks[hit.rigidbody.GetAttachedOWRigidbody()][i].Item3 == target.transform)
                            {
                                placedBlocks[hit.rigidbody.GetAttachedOWRigidbody()].RemoveAt(i);
                                blockFound = true;
                                break;
                            }
                        }
                        if (!blockFound)
                            ModHelper.Console.WriteLine("No block found to remove", OWML.Common.MessageType.Warning);
                        Destroy(target.gameObject);
                    }
                }
                else if (placer != null && Physics.Raycast(placer.transform.position, placer.transform.forward, range))
                {
                    PlaceObjectRaycast();
                }
            }
            else if (OWInput.IsPressed(InputLibrary.rollMode) && OWInput.IsNewlyPressed(InputLibrary.toolActionSecondary) && vr || Keyboard.current.tKey.wasPressedThisFrame)
            {
                block++;
                if (block >= blockTextures.Length)
                {
                    block = 0;
                }
                text.text = "Selected block:\n" + blockTextures[block].name;
                textTimer = 1f;
            }
            if (textTimer > 0f)
            {
                textTimer -= Time.deltaTime;
                if (textTimer <= 0f)
                {
                    text.text = "";
                    textTimer = 0f;
                }
            }
            if (Keyboard.current.oKey.wasPressedThisFrame)
            {
                Save();
            }
            if (Keyboard.current.pKey.wasPressedThisFrame)
            {
                Load();
            }
        }

        void PlaceObjectRaycast()
        {
            if (IsPlaceable(/*out Vector3 placeNormal,*/ out Vector3 placePoint, out OWRigidbody targetRigidbody))
            {
                GameObject go = MakeCube(block, true /*targetRigidbody*/);
                PlaceObject(/*placeNormal,*/ placePoint, go, targetRigidbody, block);
            }
        }

        bool IsPlaceable(/*out Vector3 placeNormal,*/ out Vector3 placePoint, out OWRigidbody targetRigidbody)
        {
            //placeNormal = Vector3.zero;
            placePoint = Vector3.zero;
            targetRigidbody = null;

            Vector3 forward = placer.transform.forward;
            if (Physics.Raycast(placer.transform.position, forward, out RaycastHit hit, range, OWLayerMask.physicalMask | OWLayerMask.interactMask))
            {
                //placeNormal = hit.normal;
                float back = 0.1f;
                if (hit.collider.name == "cube")
                    back = 0.001f;
                placePoint = hit.point - forward * back;
                targetRigidbody = hit.collider.GetAttachedOWRigidbody(false);
                placePoint = Round(targetRigidbody.transform.InverseTransformPoint(placePoint));
                Vector3Int intPos = Vector3Int.RoundToInt(placePoint);
                if (placedBlocks.ContainsKey(targetRigidbody))
                    foreach (var blk in placedBlocks[targetRigidbody])
                        if (blk.Item1 == intPos)
                            return false;
                return true;
            }
            return false;
        }

        public void PlaceObject(/*Vector3 normal,*/ Vector3 point, GameObject gameObject, OWRigidbody targetRigidbody, int blockIdx)
        {
            Transform parent = targetRigidbody.transform;
            gameObject.SetActive(true);
            gameObject.transform.SetParent(parent);
            gameObject.transform.localPosition = point;
            //if (targetRigidbody.name.Equals(gameObject.name)) {
            gameObject.transform.rotation = parent.rotation;
            //} else {
            //    gameObject.transform.rotation = Quaternion.LookRotation(normal, targetRigidbody.transform.up);
            //}

            if (gameObject.GetComponentInChildren<OWCollider>() != null)
            {
                gameObject.GetComponentInChildren<OWCollider>().SetActivation(true);
                gameObject.GetComponentInChildren<OWCollider>().enabled = true;
            }

            try
            {
                placedBlocks.Add(targetRigidbody, new List<(Vector3Int, int, Transform)>() { (Vector3Int.RoundToInt(point), blockIdx, gameObject.transform) });
            }
            catch
            {
                placedBlocks[targetRigidbody].Add((Vector3Int.RoundToInt(point), blockIdx, gameObject.transform));
            }
        }
        public GameObject MakeCube(int blockIdx, bool sound /*float size, OWRigidbody targetRigidbody*/)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (sound)
            {
                string type = "stone";
                if (blockTextures[blockIdx].name.Equals("oak_planks"))
                {
                    type = "wood";
                }
                else if (blockTextures[blockIdx].name.Equals("dirt"))
                {
                    type = "gravel";
                }
                List<AudioClip> clips = blockAudio[type];
                audio.PlayOneShot(clips[Random.Range(0, clips.Count - 1)], 1f);
            }
            //Vector3 fwd = transform.TransformDirection(Vector3.forward);
            cube.name = "cube";
            MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
            renderer.material.mainTexture = blockTextures[blockIdx];
            /*
            renderer.material.EnableKeyword("_NORMALMAP");
            renderer.material.EnableKeyword("_METALLICGLOSSMAP");
            
            renderer.material.SetTexture("_BumpMap", m_Normal);
            renderer.material.SetTexture("_MetallicGlossMap", m_Metal);*/

            renderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            renderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            renderer.material.SetInt("_ZWrite", 0);
            renderer.material.DisableKeyword("_ALPHATEST_ON");
            renderer.material.EnableKeyword("_ALPHABLEND_ON");
            renderer.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            renderer.material.renderQueue = 3000;

            return cube;
        }

        
        void Save()
        {
            Dictionary<string, List<(SVector3Int, int)>> placedBlocksSave = new();
            foreach (var rb in placedBlocks)
            {
                string objPath = GetPath(rb.Key.gameObject);
                if (placedBlocksSave.ContainsKey(objPath))
                {
                    ModHelper.Console.WriteLine("Multiple objects with path: " + rb.Key + "\nIgnoring all but the first", OWML.Common.MessageType.Warning);
                    continue;
                }
                List<(SVector3Int, int)> list = new();
                foreach (var blk in rb.Value)
                    list.Add((blk.Item1, blk.Item2));
                placedBlocksSave[objPath] = list;
            }

            var binaryFormatter = new BinaryFormatter();
            var fi = new FileInfo(ModHelper.Manifest.ModFolderPath + "save.bin");

            using (var binaryFile = fi.Create())
            {
                binaryFormatter.Serialize(binaryFile, placedBlocksSave);
                binaryFile.Flush();
            }
            ModHelper.Console.WriteLine("Blocks saved!");
            text.text = "Blocks saved!";
            textTimer = 1f;
        }

        void Load()
        {
            var binaryFormatter = new BinaryFormatter();
            var fi = new FileInfo(ModHelper.Manifest.ModFolderPath + "save.bin");

            Dictionary<string, List<(SVector3Int, int)>> placedBlocksSave = new();
            using (var binaryFile = fi.OpenRead())
            {
                placedBlocksSave = (Dictionary<string, List<(SVector3Int, int)>>)binaryFormatter.Deserialize(binaryFile);
            }

            foreach (var rb in placedBlocks)
            {
                foreach (var blk in rb.Value)
                {
                    if (blk.Item3 != null)
                        Destroy(blk.Item3.gameObject);
                }
            }

            placedBlocks.Clear();

            foreach (var rb in placedBlocksSave)
            {
                GameObject thing = GameObject.Find(rb.Key);
                if (thing == null)
                {
                    ModHelper.Console.WriteLine("Failed to load blocks on nonexistant object: " + rb.Key, OWML.Common.MessageType.Warning);
                    continue;
                }
                OWRigidbody rigidbody = thing.GetComponent<OWRigidbody>();
                List<(Vector3Int, int, Transform)> list = new();
                foreach (var blk in rb.Value)
                {
                    int blockID = blk.Item2;
                    if (blockID >= blockTextures.Length)
                    {
                        blockID = 0;
                        ModHelper.Console.WriteLine("Failed to load unkown block, defaulting to " + blockTextures[blockID].name, OWML.Common.MessageType.Warning);
                    }
                    GameObject go = MakeCube(blockID, false);
                    PlaceObject(blk.Item1, go, rigidbody, blockID);
                    list.Add((blk.Item1, blockID, go.transform));
                }
                placedBlocks[rigidbody] = list;
            }
            ModHelper.Console.WriteLine("Blocks loaded!");
            text.text = "Blocks loaded!";
            textTimer = 1f;
        }

        public static Vector3 Round(Vector3 vector3, int decimalPlaces = 0)
        {
            float multiplier = 1;
            for (int i = 0; i < decimalPlaces; i++)
            {
                multiplier *= 10f;
            }
            return new Vector3(
                Mathf.Round(vector3.x * multiplier) / multiplier,
                Mathf.Round(vector3.y * multiplier) / multiplier,
                Mathf.Round(vector3.z * multiplier) / multiplier);
        }

        public Texture2D GetTexture(string path)
        {
            var data = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(data);
            return texture;
        }

        public string GetFilename(string path)
        {
            int slash = path.LastIndexOf("\\") + 1;
            int dot = path.LastIndexOf(".");
            return path.Substring(slash, dot - slash);
        }

        public static string GetPath(GameObject go)
        {
            string name = go.name;
            while (go.transform.parent != null)
            {

                go = go.transform.parent.gameObject;
                name = go.name + "/" + name;
            }
            return name;
        }
    }
}

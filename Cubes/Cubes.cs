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
using System.Linq;
using OWML.Common;
using QSB.Messaging;

namespace Cubes
{
    public class Cubes : ModBehaviour
    {
        readonly float range = 5f;
        protected AudioSource audio;
        Dictionary<string, Material> blockMaterials = new();
        Dictionary<string, List<AudioClip>> blockAudio = new();
        int block = 0;
        FirstPersonManipulator placer;
        bool vr = false;
        Dictionary<OWRigidbody, List<(Vector3Int, string, Transform)>> placedBlocks = new();
        Text text;
        float textTimer = 0f;
        public static IModConsole modConsole;
        public static Cubes I { get; private set; }

        private void Start()
        {
            LoadManager.OnCompleteSceneLoad += (scene, loadScene) =>
            {
                I = this;
                modConsole = ModHelper.Console;
                if (loadScene != OWScene.SolarSystem) return;
                audio = GameObject.Find("Player_Body/Audio_Player/OneShotAudio_Player").GetComponent<AudioSource>();
                string[] fileEntries = Directory.GetFiles(ModHelper.Manifest.ModFolderPath + "blocks");
                Dictionary<string, (Texture2D, Texture2D, Texture2D)> blockTextures = new();
                for (int i = 0; i < fileEntries.Length; i++)
                {
                    string path = fileEntries[i];
                    string fileName = GetFilename(path);
                    if (path.EndsWith("_n.png"))
                    {
                        fileName = fileName.Substring(0, fileName.Length - 2);
                        blockTextures[fileName] = (blockTextures[fileName].Item1, GetTexture(path, true), blockTextures[fileName].Item3);
                    } else if (path.EndsWith("_s.png"))
                    {
                        fileName = fileName.Substring(0, fileName.Length - 2);
                        blockTextures[fileName] = (blockTextures[fileName].Item1, blockTextures[fileName].Item2, GetTexture(path));
                    } else
                    {
                        blockTextures[fileName] = (GetTexture(path), null, null);
                    }
                }
                MeshRenderer renderer = GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<MeshRenderer>();
                Material mat = renderer.material;
                Destroy(renderer.gameObject);
                foreach (var blk in blockTextures)
                {
                    Material material = new Material(mat);
                    material.mainTexture = blk.Value.Item1;
                    if (blk.Value.Item2 != null)
                    {
                        material.EnableKeyword("_NORMALMAP");
                        material.SetTexture("_BumpMap", blk.Value.Item2);
                    }
                    if (blk.Value.Item3 != null)
                    {
                        material.EnableKeyword("_METALLICGLOSSMAP");
                        material.SetTexture("_MetallicGlossMap", blk.Value.Item3);
                    }

                    material.SetOverrideTag("RenderType", "TransparentCutout");
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    material.SetInt("_ZWrite", 1);
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.DisableKeyword("_ALPHABLEND_ON");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;

                    material.enableInstancing = true;

                    blockMaterials[blk.Key] = material;
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
                        DestroyBlock(hit.rigidbody.GetAttachedOWRigidbody(), target);
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
                if (block >= blockMaterials.Count)
                {
                    block = 0;
                }
                text.text = "Selected block:\n" + blockMaterials.ElementAt(block).Key;
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
                GameObject go = MakeCube(blockMaterials.ElementAt(block).Key, true /*targetRigidbody*/);
                PlaceObject(/*placeNormal,*/ placePoint, go, targetRigidbody, blockMaterials.ElementAt(block).Key);
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

        public void PlaceObject(/*Vector3 normal,*/ Vector3 point, GameObject gameObject, OWRigidbody targetRigidbody, string blockName, bool remote = false)
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
                placedBlocks.Add(targetRigidbody, new List<(Vector3Int, string, Transform)>() { (Vector3Int.RoundToInt(point), blockName, gameObject.transform) });
            }
            catch
            {
                placedBlocks[targetRigidbody].Add((Vector3Int.RoundToInt(point), blockName, gameObject.transform));
            }
            if (!remote)
                new BlockPlacedMessage(GetPath(targetRigidbody.gameObject), Vector3Int.RoundToInt(point), blockName).Send();
        }
        public GameObject MakeCube(string blockName, bool sound /*float size, OWRigidbody targetRigidbody*/)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (sound)
            {
                string type = "stone";
                if (blockName.Equals("oak_planks"))
                {
                    type = "wood";
                }
                else if (blockName.Equals("dirt"))
                {
                    type = "gravel";
                }
                List<AudioClip> clips = blockAudio[type];
                audio.PlayOneShot(clips[Random.Range(0, clips.Count - 1)], 1f);
            }
            //Vector3 fwd = transform.TransformDirection(Vector3.forward);
            cube.name = "cube";
            MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = blockMaterials[blockName];

            return cube;
        }

        void DestroyBlock(OWRigidbody rb, GameObject go, Vector3Int pos = default, bool remote = false)
        {
            bool blockFound = false;
            for (int i = 0; i < placedBlocks[rb].Count; i++)
            {
                if (!remote && placedBlocks[rb][i].Item3 == go.transform || remote && placedBlocks[rb][i].Item1 == pos)
                {
                    if (!remote)
                    {
                        new BlockPlacedMessage(GetPath(rb.gameObject), placedBlocks[rb][i].Item1, "").Send();
                    }
                    else
                    {
                        go = placedBlocks[rb][i].Item3.gameObject;
                    }
                    placedBlocks[rb].RemoveAt(i);
                    blockFound = true;
                    break;
                }
            }
            if (!blockFound)
                ModHelper.Console.WriteLine("No block found to remove", OWML.Common.MessageType.Warning);
            Destroy(go);
        }

        void DestroyAll()
        {
            foreach (var rb in placedBlocks)
            {
                foreach (var blk in rb.Value)
                {
                    if (blk.Item3 != null)
                    {
                        Destroy(blk.Item3.gameObject);
                    }
                }
            }
            placedBlocks.Clear();
        }

        public void PlaceRemoteBlock(string path, Vector3Int pos, string blockName, bool destroyAll = false)
        {
            if (destroyAll)
            {
                DestroyAll();
                return;
            }
            GameObject thing = GameObject.Find(path);
            if (thing == null)
            {
                ModHelper.Console.WriteLine("Failed to place block on nonexistant object: " + path, OWML.Common.MessageType.Warning);
                return;
            }
            OWRigidbody rigidbody = thing.GetComponent<OWRigidbody>();
            if (!blockMaterials.ContainsKey(blockName))
            {
                if (blockName == "")
                {
                    ModHelper.Console.WriteLine("Received destruction " + blockName);
                    DestroyBlock(rigidbody, null, pos, true);
                    return;
                }
                string oldName = blockName;
                blockName = blockMaterials.ElementAt(0).Key;
                ModHelper.Console.WriteLine("Failed to load unkown block \"" + oldName + "\", defaulting to " + blockName, OWML.Common.MessageType.Warning);
            }
            GameObject gobj = MakeCube(blockName, false);
            PlaceObject(pos, gobj, rigidbody, blockName, true);
        }
        
        void Save(Dictionary<string, List<(SVector3Int, string)>> placedBlocksSave = null)
        {
            if (placedBlocksSave == null)
            {
                placedBlocksSave = new();
                foreach (var rb in placedBlocks)
                {
                    string objPath = GetPath(rb.Key.gameObject);
                    if (placedBlocksSave.ContainsKey(objPath))
                    {
                        ModHelper.Console.WriteLine("Multiple objects with path: " + rb.Key + "\nIgnoring all but the first", OWML.Common.MessageType.Warning);
                        continue;
                    }
                    List<(SVector3Int, string)> list = new();
                    foreach (var blk in rb.Value)
                        list.Add((blk.Item1, blk.Item2));
                    placedBlocksSave[objPath] = list;
                }
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

        void Load(bool retry = false)
        {
            if (!File.Exists(ModHelper.Manifest.ModFolderPath + "save.bin"))
            {
                ModHelper.Console.WriteLine("No save to load");
                return;
            }
            var binaryFormatter = new BinaryFormatter();
            var fi = new FileInfo(ModHelper.Manifest.ModFolderPath + "save.bin");

            Dictionary<string, List<(SVector3Int, string)>> placedBlocksSave = new();
            try
            {
                using (var binaryFile = fi.OpenRead())
                {
                    placedBlocksSave = (Dictionary<string, List<(SVector3Int, string)>>)binaryFormatter.Deserialize(binaryFile);
                }
            }
            catch
            {
                if (!retry)
                {
                    ModHelper.Console.WriteLine("Legacy save detected");
                    ConvertLegacySave();
                    return;
                }
                else
                {
                    ModHelper.Console.WriteLine("Your save's broken", OWML.Common.MessageType.Error);
                }
            }

            DestroyAll();
            new BlockPlacedMessage(null, Vector3Int.zero, null, true).Send();

            foreach (var rb in placedBlocksSave)
            {
                GameObject thing = GameObject.Find(rb.Key);
                if (thing == null)
                {
                    ModHelper.Console.WriteLine("Failed to load blocks on nonexistant object: " + rb.Key, OWML.Common.MessageType.Warning);
                    continue;
                }
                OWRigidbody rigidbody = thing.GetComponent<OWRigidbody>();
                List<(Vector3Int, string, Transform)> list = new();
                foreach (var blk in rb.Value)
                {
                    string blockName = blk.Item2;
                    if (!blockMaterials.ContainsKey(blockName))
                    {
                        blockName = blockMaterials.ElementAt(0).Key;
                        ModHelper.Console.WriteLine("Failed to load unkown block \"" + blk.Item2 + "\", defaulting to " + blockName, OWML.Common.MessageType.Warning);
                    }
                    GameObject go = MakeCube(blockName, false);
                    PlaceObject(blk.Item1, go, rigidbody, blockName);
                    list.Add((blk.Item1, blockName, go.transform));
                }
                placedBlocks[rigidbody] = list;
            }
            ModHelper.Console.WriteLine("Blocks loaded!");
            text.text = "Blocks loaded!";
            textTimer = 1f;
        }

        void ConvertLegacySave()
        {
            var binaryFormatter = new BinaryFormatter();
            var fi = new FileInfo(ModHelper.Manifest.ModFolderPath + "save.bin");

            Dictionary<string, List<(SVector3Int, int)>> placedBlocksSaveLegacy = new();
            using (var binaryFile = fi.OpenRead())
            {
                placedBlocksSaveLegacy = (Dictionary<string, List<(SVector3Int, int)>>)binaryFormatter.Deserialize(binaryFile);
            }

            Dictionary<string, List<(SVector3Int, string)>> placedBlocksSaveNew = new();
            foreach (var rb in placedBlocksSaveLegacy)
            {
                List<(SVector3Int, string)> list = new();
                foreach (var blk in rb.Value)
                {
                    string blockName;
                    try
                    {
                        blockName = blockMaterials.ElementAt(blk.Item2).Key;
                    } catch
                    {
                        blockName = blockMaterials.ElementAt(0).Key;
                        ModHelper.Console.WriteLine("Failed to load unkown block, defaulting to " + blockName, OWML.Common.MessageType.Warning);
                    }
                    list.Add((blk.Item1, blockName));
                }
                placedBlocksSaveNew[rb.Key] = list;
            }

            ModHelper.Console.WriteLine("Upgrading save...");
            Save(placedBlocksSaveNew);
            Load(true);
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

        public Texture2D GetTexture(string path, bool linear = false)
        {
            var data = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear);
            texture.LoadImage(data);
            texture.filterMode = FilterMode.Point;
            texture.name = GetFilename(path);
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

using OWML.Common;
using OWML.ModHelper;
using UnityEngine;
using UnityEngine.InputSystem;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine.Networking;

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
                        Object.DontDestroyOnLoad(clip);
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
            yield return new WaitForEndOfFrame();
            placer = FindObjectOfType<FirstPersonManipulator>();
            if (placer.gameObject != Locator.GetPlayerCamera().gameObject)
            {
                vr = true;
            }
        }

        private void Update()
        {
            if (OWInput.IsNewlyPressed(InputLibrary.lockOn) && (!vr || placer._interactReceiver == null && placer._interactZone == null && OWInput.IsInputMode(InputMode.Character)))
            {
                if ((OWInput.IsPressed(InputLibrary.freeLook) || OWInput.IsPressed(InputLibrary.rollMode) && vr) && Physics.Raycast(placer.transform.position, placer.transform.forward, out RaycastHit hit, range, OWLayerMask.physicalMask | OWLayerMask.interactMask))
                {
                    GameObject targetRigidbody = hit.collider.gameObject;
                    if (targetRigidbody.name.Equals("cube"))
                    {
                        Destroy(targetRigidbody.gameObject);
                    }
                }
                else if (Physics.Raycast(placer.transform.position, placer.transform.forward, range))
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
            }
        }

        void PlaceObjectRaycast()
        {
            if (IsPlaceable(/*out Vector3 placeNormal,*/ out Vector3 placePoint, out OWRigidbody targetRigidbody))
            {
                GameObject go = MakeCube(/*targetRigidbody*/);
                PlaceObject(/*placeNormal,*/ placePoint, go, targetRigidbody);
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
                placePoint = hit.point - forward * 0.1f;
                targetRigidbody = hit.collider.GetAttachedOWRigidbody(false);
                return true;
            }
            return false;
        }

        public void PlaceObject(/*Vector3 normal,*/ Vector3 point, GameObject gameObject, OWRigidbody targetRigidbody)
        {
            Transform parent = targetRigidbody.transform;
            gameObject.SetActive(true);
            gameObject.transform.SetParent(parent);
            gameObject.transform.position = point + gameObject.transform.TransformDirection(Vector3.zero);
            gameObject.transform.localPosition = Round(gameObject.transform.localPosition);
            //if (targetRigidbody.name.Equals(gameObject.name)) {
            gameObject.transform.rotation = targetRigidbody.transform.rotation;
            //} else {
            //    gameObject.transform.rotation = Quaternion.LookRotation(normal, targetRigidbody.transform.up);
            //}

            if (gameObject.GetComponentInChildren<OWCollider>() != null)
            {
                gameObject.GetComponentInChildren<OWCollider>().SetActivation(true);
                gameObject.GetComponentInChildren<OWCollider>().enabled = true;
            }
        }
        public GameObject MakeCube(/*float size, OWRigidbody targetRigidbody*/)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            string type = "stone";
            if (blockTextures[block].name.Equals("oak_planks"))
            {
                type = "wood";
            }
            else if (blockTextures[block].name.Equals("dirt"))
            {
                type = "gravel";
            }
            List<AudioClip> clips = blockAudio[type];
            audio.PlayOneShot(clips[Random.Range(0, clips.Count-1)], 1f);
            //Vector3 fwd = transform.TransformDirection(Vector3.forward);
            cube.name = "cube";
            MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
            renderer.material.mainTexture = blockTextures[block];
            /*
            renderer.material.EnableKeyword("_NORMALMAP");
            renderer.material.EnableKeyword("_METALLICGLOSSMAP");
            
            renderer.material.SetTexture("_BumpMap", m_Normal);
            renderer.material.SetTexture("_MetallicGlossMap", m_Metal);*/

            return cube;
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
        /*
        public static Texture2D LoadImage(string filepath)
        {
            Texture2D tex = new(2, 2);
            tex.LoadRawTextureData(File.ReadAllBytes(filepath));
            return tex;
        }*/

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
    }
}

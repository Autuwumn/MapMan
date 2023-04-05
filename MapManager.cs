using System;
using System.Linq;
using System.Resources;
using BepInEx;
using HarmonyLib;
using R3DCore.Menu;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using R3DCore;
using System.Collections.Generic;
using UnityEngine.Rendering;
using Jotunn.Utils;
using UnityEngine.Experimental.Rendering;
using System.Collections;

namespace R3DCore.Maps
{
    [BepInPlugin(ModId, ModName, Version)]
    [BepInProcess("ROUNDS 3D.exe")]
    [HarmonyPatch]
    public class MapMan : BaseUnityPlugin
    {
        private const string ModId = "koala.map.manager";
        private const string ModName = "MM";
        public const string Version = "0.0.0";

        public static MapMan instance;
        public int curMap = 0;

        public static List<string> Maps = new List<string>() { "null" };

        private void Awake()
        {
            DontDestroyOnLoad(this);
            new Harmony(ModId).PatchAll();
            instance = this;
        }
        private void Start()
        {
            SceneManager.GetSceneByName("SimpleScene");
            RegisterMap(AssetUtils.LoadAssetBundleFromResources("koalasmaps", typeof(MapMan).Assembly).GetAllScenePaths().First<string>());
        }
        public void RegisterMap(string map)
        {
            Maps.Add(map);
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "Start")]
        public static bool PatchStart(Player __instance)
        {
            __instance.gameObject.AddComponent<MapPhotonManager>();
            return false;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Connection), "OnPlayerEnteredRoom")]
        public static bool PatchOnPlayerEnteredRoom(Player newPlayer)
        {
            if(PhotonNetwork.IsMasterClient)
            {
                var view = FindObjectsOfType<PhotonView>().Where((pv) => pv.IsMine == true && pv.gameObject.GetComponent<Player>()).ToArray()[0];
                view.RPC("RPCPushMap", RpcTarget.All, instance.curMap);
            }
            return false;
        }
        public void NextMap()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            var nextMap = UnityEngine.Random.Range(0, Maps.Count - 1);
            var view = FindObjectsOfType<PhotonView>().Where((pv) => pv.IsMine == true && pv.gameObject.GetComponent<Player>()).ToArray()[0];
            if (nextMap == instance.curMap) nextMap++;
            view.RPC("RPCPushMap", RpcTarget.All, nextMap);
        }
        public void LoadMap(int index)
        {
            for(int i = 1; i < Maps.Count; i++)
            {
                try
                {
                    SceneManager.UnloadSceneAsync(Maps[i]);
                } catch { }
            }
            if (index == 0) 
            {
                var objs = Resources.FindObjectsOfTypeAll<GameObject>().Where((o) => o.name == "Map" || o.name == "Mo_Castles").ToArray();
                objs[0].SetActive(true);
                objs[1].SetActive(true);
            }
            else
            {
                try
                {
                    GameObject.Find("Map").SetActive(false);
                    GameObject.Find("Mo_Castles").SetActive(false);
                } catch { }
                SceneManager.LoadSceneAsync(Maps[index], LoadSceneMode.Additive);
                SceneManager.sceneLoaded += (Scene scene, LoadSceneMode Mode)=> { TextureMap(); };
            }
        }
        public void TextureMap()
        {
            var mat = Resources.Load<GameObject>("Player").transform.Find("Collider_Hitbox").GetComponent<MeshRenderer>().material;
            foreach(var ren in GameObject.Find("CustomMap").GetComponentsInChildren<Renderer>())
            {
                var newMat = new Material(mat);
                newMat.color = ren.material.color;
                ren.material = newMat;
            }
        }
    }

    public class MapPhotonManager : MonoBehaviourPunCallbacks
    {
        [PunRPC]
        public void RPCPushMap(int map)
        {
            MapMan.instance.LoadMap(map);
            var view = FindObjectsOfType<PhotonView>().Where((pv) => pv.IsMine == true).ToArray()[0];
            MapMan.instance.curMap = map;
        }
    }
}
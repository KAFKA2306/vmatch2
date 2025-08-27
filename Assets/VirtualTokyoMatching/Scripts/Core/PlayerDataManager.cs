using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using System;

namespace VirtualTokyoMatching
{
    /*──────────────────────────────
     *  PlayerDataManager
     *  – VRChat Persistence API 版 –
     *──────────────────────────────
     * 1. Player Object の子に本スクリプトと
     *    「VRC Enable Persistence」を追加
     * 2. BehaviourSyncMode.Manual なので
     *    Push() 内の RequestSerialization() で
     *    必要な時だけ同期します
     */
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PlayerDataManager : UdonSharpBehaviour
    {
    /*──────── ① 同期フィールド ────────*/
        [UdonSynced] private int   progress   = 0;      // 0-112
        [UdonSynced] private int   flags      = 0;      // bit-flags
        [UdonSynced] private int   lastActive = 0;      // Unix 秒

        [UdonSynced] private int[]   answers  = new int[112];
        [UdonSynced] private float[] vector30 = new float[30];

    /*──────── ② ローカル状態 ────────*/
        [SerializeField] private bool isLoaded = false;

    /*──────── ③ 公開プロパティ ────────*/
        public bool  IsLoaded  => isLoaded;
        public int   Progress  => progress;
        public int   Flags     => flags;
        public bool  PublicShare      => (flags & 1) != 0;
        public bool  ProvisionalShare => (flags & 2) != 0;

    /*──────── ④ 初期化 ────────*/
        void Start()
        {
            if (answers   == null || answers.Length   != 112) answers   = new int[112];
            if (vector30  == null || vector30.Length  != 30)  vector30  = new float[30];
        }

    /*──────── ⑤ 静的ファインダ ────────*/
        public static PlayerDataManager Find(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player)) return null;
            GameObject[] objs = Networking.GetPlayerObjects(player);
            if (objs == null) return null;

            foreach (GameObject o in objs)
            {
                PlayerDataManager pdm = o.GetComponentInChildren<PlayerDataManager>();
                if (Utilities.IsValid(pdm)) return pdm;
            }
            return null;
        }

    /*──────── ⑥ データ操作 ────────*/
        public void SaveResponse(int index, int value)
        {
            if (index < 0 || index >= 112 || value < 1 || value > 5) return;

            answers[index] = value;

            int answered = 0;
            for (int i = 0; i < 112; i++)
                if (answers[i] > 0) answered++;
            progress = answered;

            Push();
            Debug.Log($"[PDM] Q{index + 1} = {value}  Progress {progress}/112");
        }

        public int[] GetAllResponses()
        {
            int[] copy = new int[112];
            Array.Copy(answers, copy, 112);
            return copy;
        }

        public int GetNextUnanswered()
        {
            for (int i = 0; i < 112; i++)
                if (answers[i] == 0) return i;
            return -1;
        }

        public bool  IsComplete()        => progress >= 112;
        public float GetCompletionRatio() => (float)progress / 112f;

        public void UpdateVector30(float[] newVec)
        {
            if (newVec == null || newVec.Length != 30) return;
            Array.Copy(newVec, vector30, 30);
            Push();
        }

        public float[] GetVector30()
        {
            float[] copy = new float[30];
            Array.Copy(vector30, copy, 30);
            return copy;
        }

        public void SetFlags(int newFlags)
        {
            flags = newFlags;
            Push();
        }

        public void ResetAllData()
        {
            progress = 0;
            flags    = 0;
            lastActive = 0;

            for (int i = 0; i < 112; i++) answers[i]  = 0;
            for (int i = 0; i < 30;  i++) vector30[i] = 0f;

            Push();
            Debug.Log("[PDM] All data reset");
        }

    /*──────── ⑦ 永続化ヘルパ ────────*/
        private void Push()
        {
            lastActive = (int)(DateTime.UtcNow.Subtract(new DateTime(1970,1,1)).TotalSeconds);
            RequestSerialization();   // Persistence へ即書き込み
        }

        public override void OnPlayerRestored(VRCPlayerApi player)
        {
            if (player.isLocal) isLoaded = true;
        }

    /*──────────────────────────────
     *  ↓ 旧 API 互換レイヤー ↓
     *──────────────────────────────*/
        #region LegacyAPI

        // 旧プロパティ
        public bool  IsDataLoaded                => IsLoaded;
        public int   CurrentProgress             => Progress;
        public bool  IsPublicSharingEnabled      => PublicShare;
        public bool  IsProvisionalSharingEnabled => ProvisionalShare;

        // 旧メソッド
        public int[]  GetAllQuestionResponses()   => GetAllResponses();
        public int    GetNextUnansweredQuestion() => GetNextUnanswered();
        public bool   IsAssessmentComplete()      => IsComplete();
        public float  GetCompletionPercentage()   => GetCompletionRatio();
        public float[] GetVector30D()             => GetVector30();
        public void    UpdateVector30D(float[] v) => UpdateVector30(v);
        public void    SaveQuestionResponse(int i, int v) => SaveResponse(i, v);
        public void    SavePlayerData() { }              // 旧呼び出しを無害化
        public void    ResetPlayerData() => ResetAllData();

        #endregion
    }
}

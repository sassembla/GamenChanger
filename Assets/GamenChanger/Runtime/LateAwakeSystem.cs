using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.LowLevel;

namespace GamenChangerCore
{
    public class LateAwakeSystem
    {
        private List<Corner> uninitialized = new List<Corner>();
        private void LateAwake()
        {
            // oneOfNCornerの初期化が末尾に来るような初期化順制御を行う。
            var oneOfNCorners = uninitialized.Where(t => t is OneOfNCorner).ToArray();
            uninitialized.RemoveAll(t => oneOfNCorners.Contains(t));
            uninitialized.AddRange(oneOfNCorners);

            while (true)
            {
                try
                {
                    var uninitialize = uninitialized[0];
                    if (uninitialize != null)
                    {
                        uninitialize.ReloadCorner();
                    }
                }
                catch
                {
                    Debug.LogError("failed to execute ReloadCorner of:" + uninitialized[0]);
                }

                uninitialized.RemoveAt(0);

                if (uninitialized.Count == 0)
                {
                    break;
                }
            }

            // LateAwakeの登録を消す処理
            {
                var currentPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();

                var currentPlayerLoopSubSystems = currentPlayerLoop.subSystemList;
                var done = false;

                // サブシステムから、Cornerの初期化タイミング導入解除を、このハンドラ自体の登録ごと削除する。
                for (var i = 0; i < currentPlayerLoopSubSystems.Length; i++)
                {
                    var system = currentPlayerLoopSubSystems[i];

                    if (system.type == typeof(UnityEngine.PlayerLoop.Initialization))
                    {
                        // HookClass関連を取り除く
                        var targetSystemListWithoutCorner = system.subSystemList.ToList().Where(sys => sys.type != typeof(LateAwakeSystem)).ToArray();

                        // 取り除いたもので上書き
                        currentPlayerLoopSubSystems[i].subSystemList = targetSystemListWithoutCorner.ToArray();

                        done = true;
                        break;
                    }
                }

                // 書き換えが終わったら更新
                if (done)
                {
                    PlayerLoop.SetPlayerLoop(currentPlayerLoop);
                }
            }
        }

        private static LateAwakeSystem hookObject = new LateAwakeSystem();

        private LateAwakeSystem() { }

        public static void SetupLateAwake(Corner corner)
        {
            // 初期化順リストに追加
            hookObject.uninitialized.Add(corner);

            // 現行のplayerLoopにLateAwakeをcallするタイミングを追加
            var currentPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            var currentPlayerLoopSubSystems = currentPlayerLoop.subSystemList;

            for (var i = 0; i < currentPlayerLoopSubSystems.Length; i++)
            {
                var system = currentPlayerLoopSubSystems[i];

                if (system.type == typeof(UnityEngine.PlayerLoop.Initialization))
                {
                    var targetSystemList = system.subSystemList.ToList();

                    if (targetSystemList.Where(sys => sys.type == typeof(LateAwakeSystem)).Any())
                    {
                        // すでにセットされているので何もしない
                        return;
                    }

                    var initializer = new PlayerLoopSystem()
                    {
                        type = typeof(LateAwakeSystem),
                        updateDelegate = hookObject.LateAwake
                    };

                    // 先頭に導入 initializer が先頭に来るようにすると、このフレームで着火する。
                    targetSystemList.Insert(0, initializer);

                    // 上書き
                    currentPlayerLoopSubSystems[i].subSystemList = targetSystemList.ToArray();
                    PlayerLoop.SetPlayerLoop(currentPlayerLoop);

                    // セット完了したので終了する
                    return;
                }
            }
        }
    }
}
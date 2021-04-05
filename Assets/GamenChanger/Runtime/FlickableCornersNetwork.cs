using System;
using System.Collections.Generic;
using UnityEngine;

namespace GamenChangerCore
{
    public class FlickableCornersNetwork
    {
        private List<FlickableCorner> network = new List<FlickableCorner>();
        internal void Join(FlickableCorner flickableCorner)
        {
            if (network.Contains(flickableCorner))
            {
                return;
            }

            Debug.Log("足してる flickableCorner:" + flickableCorner);

            network.Add(flickableCorner);
        }

        // タッチ開始通知が発生したので、現在タッチを保持している別のFlickableCornerのタッチの調整を行う。
        // 実際にはtouchIdとかがどうなってるのか見たりする。まあ調停コーナだなここは。
        internal bool FlickInitializeRequest(string cornerId, int touchId)
        {
            // ここで、touchIdは画面内ですべて連番になっていると言うのがわかった。
            // なので、今アクティブなやつ、というのは一意にできる。先発のやつをdeactivateすればいいんだ。identity消したことにすればいい。
            // 誰か一人でもアニメーション中だったらrejectしたいところ
            foreach (var f in network)
            {
                // 発生対象自身であれば無視する
                if (f.CornerId == cornerId)
                {
                    continue;
                }

                if (f.HasActiveFlick())
                {
                    if (f.IsAnimating())
                    {
                        return false;
                    }
                    // TODO: 暫定的に、引き出されつつある状態のFlickableViewへのタッチを認めていない。
                    // f.InactivateCurrentFlickIdentity();
                    return false;
                }
            }
            return true;
        }

        internal bool FlickAwakeRequest(string cornerId, int touchId)
        {
            foreach (var f in network)
            {
                // 発生対象自身であれば無視する
                if (f.CornerId == cornerId)
                {
                    continue;
                }

                if (f.HasActiveFlick())
                {
                    if (f.IsAnimating())
                    {
                        return false;
                    }
                    // TODO: 暫定的に、引き出されつつある状態のFlickableViewへのタッチを認めていない。
                    // f.InactivateCurrentFlickIdentity();
                    return false;
                }
            }
            return true;
        }
    }
}
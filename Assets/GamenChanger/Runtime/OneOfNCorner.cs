using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GamenChangerCore
{
    public class OneOfNCorner : Corner, IPointerUpHandler
    {
        // この時点で収集は済んでるので、さてどうしたものか。接続を作るのが難しいんだよな。まあ適当に参照で繋ぐのが自由でいいなー。
        /*
            2つルートがあって、一つはダイレクト操作ルート。要素に対してのインプットを受け取ってハンドラを着火する。
            次に、それとクロスカウンターするような指定ルート。upとdownか。ループしないように、callerがこいつだったらシャットダウンしたい。できるかなーー

            あ、できるな。たぶん。callstackみたいなやつを作ればいいんだ。うーん、、、重そう、、
            サーキットブレイカーを仕込むのがよさそう。

            OneOfNCorner.UpdateTargetSet(targetSet)

            // これは開始時に自動的に収集される。コードで足す場合はGetしてAddしてSetする必要がある。
            targetSet {
             GameObject with Input/Gesture特性 / Corner特性 []
            }

            GameObject One// 現在選ばれている要素、親Cornerか、targetSet内の何かを押したらセットされる。

            // このCornerを保持するCornerがDraggableCornerで、このCornerで保持されてる要素の上でdragイベントが発生した時、次の関数が呼ばれる
            OnHoverDown(target, others)

            // このCornerを保持するCornerがDraggableCornerで、このCornerで保持されてる要素の上でdragイベントが終了した時、次の関数が呼ばれる
            OnHoverUp(target, others)


            oneはとりあえずセットさせた方が良さそう。選定は厳しい。

         */

        public GameObject One;

        public new void Awake()
        {
            base.Awake();

            if (One == null)
            {
                Debug.LogError("this:" + gameObject.name + " 's OneOfN/One is null. please set.");
                return;
            }


        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Debug.Log("eventData:" + eventData);
        }
    }
}
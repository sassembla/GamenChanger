using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GamenChangerCore
{
    // TODO: 対象となるhandlerを見つけてinspectorに表示したい感じある。
    public class OneOfNCorner : Corner
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

            // containedUIComponentsへと自動的にagentを生やす
            // TODO: もっといい方法があると思うが、、まあはい。
            foreach (var content in containedUIComponents)
            {
                var agent = content.gameObject.AddComponent<OneOfNAgent>();
                agent.parent = this;
            }

            // 親からIOneOfNCornerHandlerを探して着火する
            var listener = transform.parent.GetComponent<IOneOfNCornerHandler>();
            if (listener != null)
            {
                listener.OnInitialized(One);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            var currentReactedObject = eventData.pointerPress;

            // すでに同じオブジェクトが押された後であれば無視する
            if (currentReactedObject == One)
            {
                return;
            }

            // Oneの更新
            One = currentReactedObject;

            // 親からIOneOfNCornerHandlerを探して着火する
            var listener = transform.parent.GetComponent<IOneOfNCornerHandler>();
            if (listener != null)
            {
                listener.OnChangedToOne(One);
            }

            // 表示状態の変更を行う
            // TODO: button, text, その他、、何を
            // TODO: どう変えるか、というのを選ぶ。統一的な感じ。
            foreach (var content in containedUIComponents)
            {
                var component = content.GetComponent<Button>();

                // 対象のオブジェクトの特性を変える。チェックがついてることを全部やる。
                if (currentReactedObject == component.gameObject)
                {
                    // disableにする
                    component.interactable = false;
                    continue;
                }

                // enableにする
                component.interactable = true;
            }

            // containedUIComponents
            /*
                OnPointerUp eventData:Position: (62.0, 24.0)
                delta: (0.0, 0.0)
                eligibleForClick: True
                pointerEnter: Text (UnityEngine.GameObject)
                pointerPress: Button1 (UnityEngine.GameObject)
                lastPointerPress: 
                pointerDrag: 
                Use Drag Threshold: True
                Current Raycast:
                Name: Text (UnityEngine.GameObject)
                module: Name: Canvas (UnityEngine.GameObject)
                eventCamera: 
                sortOrderPriority: 0
                renderOrderPriority: 0
                distance: 0
                index: 0
                depth: 25
                worldNormal: (0.0, 0.0, -1.0)
                worldPosition: (0.0, 0.0, 0.0)
                screenPosition: (62.0, 24.0)
                module.sortOrderPriority: 0
                module.renderOrderPriority: 0
                sortingLayer: 0
                sortingOrder: 0
                Press Raycast:
                Name: Text (UnityEngine.GameObject)
                module: Name: Canvas (UnityEngine.GameObject)
                eventCamera: 
                sortOrderPriority: 0
                renderOrderPriority: 0
                distance: 0
                index: 0
                depth: 25
            */
        }
    }
}
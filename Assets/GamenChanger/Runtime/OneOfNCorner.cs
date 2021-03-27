using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GamenChangerCore
{
    // TODO: 対象となるhandlerを見つけてinspectorに表示したい感じある。押したら接続線出したいね、、
    public class OneOfNCorner : Corner
    {
        /*
            2つルートがあって、一つはダイレクト操作ルート。要素に対してのインプットを受け取ってハンドラを着火する。
            次に、それとクロスカウンターするような指定ルート。upとdownか。ループしないように、callerがこいつだったらシャットダウンしたい。できるかなーー

            OneOfNCorner.UpdateTargetSet(targetSet)

            // これは開始時に自動的に収集される。コードで足す場合はGetしてAddしてSetする必要がある。
            targetSet {
             GameObject with Input/Gesture特性 / Corner特性 []
            }

            GameObject One// 現在選ばれている要素、親Cornerか、targetSet内の何かを押したらセットされる。
         */

        public GameObject One;

        private IOneOfNCornerHandler listener;

        public new void Awake()
        {
            // TODO: このへんをScene上とかでチェックできるといいね。
            if (One == null)
            {
                Debug.LogError("this:" + gameObject.name + " 's OneOfN/One is null. please set before addComponent or instantiate.");
                return;
            }

            if (One.transform.parent != this.transform)
            {
                Debug.LogError("should choose one from this:" + this.gameObject + "'s child/children.");
                return;
            }

            var listenerCandidate = transform.parent.GetComponent<IOneOfNCornerHandler>();
            if (listenerCandidate != null)
            {
                // pass.
            }
            else
            {
                Debug.LogError("this:" + gameObject + " 's OneOfN handler is not found in parent:" + transform.parent.gameObject + ". please set IOneOfNCornerHandler to parent GameObject before addComponent or instantiate.");
                return;
            }

            // non nullなのを確認した上でセットする
            listener = listenerCandidate;

            // baseの初期化時に実行する関数としてこのクラスの初期化処理をセット
            base.SetSubclassReloadAction(
                () =>
                {
                    var whole = ExposureAllContents();

                    // containedUIComponentsへと自動的にagentを生やす
                    // TODO: agent勝手に付けるよりももっといい方法があると思うが、、まあはい。
                    foreach (var content in whole)
                    {
                        // すでにセット済み
                        if (content.gameObject.GetComponent<OneOfNAgent>() != null)
                        {
                            continue;
                        }

                        var agent = content.gameObject.AddComponent<OneOfNAgent>();
                        agent.parent = this;
                    }

                    // 親のOnInitializedを着火する
                    listener.OnInitialized(One, whole.Select(s => s.gameObject).ToArray());
                }
            );

            base.Awake();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            var currentReactedObject = eventData.pointerPress;

            // すでに同じオブジェクトが押された後であれば無視する
            if (currentReactedObject == One)
            {
                return;
            }

            var before = One;

            // Oneの更新
            One = currentReactedObject;

            var whole = ExposureAllContents();

            // 親のOnChangedを着火する
            listener.OnChangedToOne(One, before, whole.Select(t => t.gameObject).ToArray());
        }

        // UIを操作した副作用をlistenerに解析させ、OneOfNに反映させる
        // TODO: このメソッド微妙なので、インターフェースの方を叩かせるような作りにしたい。listener側に関数を実装させて、そっちを叩いたらうんぬんが一番良い。
        public void SelectOneWithCorner(Corner corner)
        {
            var whole = ExposureAllContents().Select(s => s.gameObject).ToArray();
            var newOne = listener.OnSelectOneOfNFromCodeWithCorner(corner, whole);

            // nullが帰ってきたら無視する。
            if (newOne == null)
            {
                return;
            }

            // すでに同じオブジェクトが押された後であれば無視する
            if (newOne == One)
            {
                return;
            }

            var before = One;

            // Oneの更新
            One = newOne;

            // 親のOnChangedを着火する
            listener.OnChangedToOne(One, before, whole);
        }

        // このOneOfNが含んでいるRectTransformを指定してOneを更新する。
        // TODO: 使い勝手が難しいので、コードで書かせるのはインターフェースが持ってる奴だけの方が良さそう。
        // public void SelectOneWithContent(RectTransform contentInOneOfN)
        // {
        //     if (!ExposureAllContents().Contains(contentInOneOfN))
        //     {
        //         return;
        //     }

        //     var newOne = contentInOneOfN.gameObject;

        //     // すでに同じオブジェクトが押された後であれば無視する
        //     if (newOne == One)
        //     {
        //         return;
        //     }

        //     var before = One;

        //     // Oneの更新
        //     One = newOne;

        //     var whole = ExposureAllContents().Select(s => s.gameObject).ToArray();

        //     // 親のOnChangedを着火する
        //     listener.OnChangedToOne(One, before, whole);
        // }
    }
}
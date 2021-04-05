using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GamenChangerCore
{
    /*
        基底のコーナー、単なる集合の箱。
        コーナーの内容を挿げ替える -> できた
        フリック可能にしてみる -> できた
        OneOfNを作ってみる -> できた
        prefabを使ったcorner生成のフローなどをつくってみる -> できた
        sequeがあるFlickableViewを作ってみる -> できた
        TODO: draggableなsequeがあるFlickableViewを作ってみる
    */
    public class Corner : MonoBehaviour
    {
        public readonly string CornerId = Guid.NewGuid().ToString();

        protected RectTransform[] containedUIComponents = null;

        public RectTransform currentRectTransform()
        {
            if (_currentRectTransform != null)
            {
                _currentRectTransform = gameObject.GetComponent<RectTransform>();
            }

            return _currentRectTransform;
        }

        private RectTransform _currentRectTransform;

        // 生成時にコンテンツを収集する。
        public void Awake()
        {
            // 自身のtransformを初期化
            _currentRectTransform = gameObject.GetComponent<RectTransform>();

            // LateAwakeを実現するシステムへと、自身を登録する。
            LateAwakeSystem.SetupLateAwake(this);
        }

        // Cornerの要素を最新化する。合わせてXCornerの最新化も行う。
        public void ReloadCorner()
        {
            ReloadContainedComponent();

            // 追加処理があれば実行する(子クラスからセットされる)
            if (additionalReloadAct != null)
            {
                additionalReloadAct();
            }
        }

        // TODO: この関数を最適化することで、高速化が見込める。まあそもそもそんな毎フレーム使わないので、別にっていう感じではある。
        // Cornerの要素を最新化する。
        public void ReloadContainedComponent()
        {
            // UIを収集/再収集する
            var childCount = transform.childCount;
            var childUIComponent = new List<RectTransform>();

            var excludedGameObjects = new List<GameObject>();

            // UIBehaviourの性質を持つコンポーネントのRectTransformを集める。
            for (var i = 0; i < childCount; i++)
            {
                var trans = transform.GetChild(i);

                // excludeされるGOであれば無視する。
                if (excludeObjects.Contains(trans.gameObject))
                {
                    excludedGameObjects.Add(trans.gameObject);
                    continue;
                }

                if (trans.TryGetComponent<UIBehaviour>(out var uiComponet))
                {
                    // excludeされるコンポーネント要素であれば無視する。
                    if (excludeTypes.Contains(uiComponet.GetType()))
                    {
                        excludedGameObjects.Add(trans.gameObject);
                        continue;
                    }

                    childUIComponent.Add(uiComponet.GetComponent<RectTransform>());
                }
            }

            containedUIComponents = childUIComponent.ToArray();

            if (excludedGameObjects.Any())
            {
                onExcludedGameObjectsFound(excludedGameObjects.ToArray());
            }
        }

        private Action additionalReloadAct;
        private Action<GameObject[]> onExcludedGameObjectsFound;
        // サブクラスからのみ使うのを想定して、cornerのreload時に追加で呼ばれるreload actionをセットする。
        protected internal void SetSubclassReloadAndExcludedAction(Action additional, Action<GameObject[]> onExcludedGameObjectFound)
        {
            this.additionalReloadAct = additional;
            this.onExcludedGameObjectsFound = onExcludedGameObjectFound;
        }



        // contentをborrowした場合の元持ち主コーナー
        private Corner originCorner;

        // 現在のCornerの内容を他のCornerのものへと書き換える。
        // 現在のCornerに収まるようにレイアウトが行われる。
        public bool TryBorrowContents(Corner corner, bool discardCurrent = false)
        {
            // originCornerが存在していてそれを意図しないborrowが発生しようとしているため、エラーログを出して失敗した旨を返す
            if (!discardCurrent && originCorner != null)
            {
                Debug.LogError("discardCurrent = false and current contents is bollowed one from originCorner:" + originCorner + ", should run BackContents() before borrow another one.");
                return false;
            }

            // 現在の中身を消す
            var childCount = transform.childCount;
            for (var i = 0; i < childCount; i++)
            {
                var trans = transform.GetChild(i);
                GameObject.Destroy(trans.gameObject);
            }

            // 対象cornerが持っているUI要素を全てこのcornerへと移し変える。borrow元となったcornerは空になる。
            var contents = corner.ExposureAllContents();

            // ここで「対象のCornerの位置」 の差に基づいて、全ての子の位置をずらす。
            var targetParentPos = corner.currentRectTransform();
            var diff = currentRectTransform().anchoredPosition - targetParentPos.anchoredPosition;

            foreach (var content in contents)
            {
                content.anchoredPosition += diff;
                var trans = content.transform;
                trans.SetParent(this.transform);
            }

            originCorner = corner;
            ReloadCorner();

            return true;
        }

        public void SwapContents(Corner targetCorner)
        {
            var temps = this.ExposureAllContents();
            foreach (var temp in temps)
            {
                temp.SetParent(this.transform.parent);
            }

            // 現在の中身はすでに何もないはずだが、なんかGameObjectが適当に置かれてるかもしれない。
            // とりあえずCornerを使う時はCornerをつけたUIの直下にはUI以外を置かないでほしいのでエラーを出して消す。
            // TODO: 条件キツすぎるかもしれないのでなんかしたほうがいいのかもしれないが、まあ後で考えよう。
            var childCount = transform.childCount;
            for (var i = 0; i < childCount; i++)
            {
                var trans = transform.GetChild(i);
                Debug.LogError("unexpected content found:" + trans.gameObject + " in corner:" + this);
                GameObject.Destroy(trans.gameObject);
            }

            // 対象cornerが持っているUI要素を全てこのcornerへと移し変える。swap元となったcornerは一旦空になる。
            var contents = targetCorner.ExposureAllContents();

            // ここで「対象のCornerの位置」 の差に基づいて、全ての子の位置をずらす。
            var baseCornerPos = targetCorner.currentRectTransform();
            var diff = currentRectTransform().anchoredPosition - baseCornerPos.anchoredPosition;

            foreach (var content in contents)
            {
                content.anchoredPosition += diff;
                var trans = content.transform;
                trans.SetParent(this.transform);
            }

            // 次に、tempsに避難しておいたcontentsをswap下のcornerに入れる。
            foreach (var content in temps)
            {
                content.anchoredPosition -= diff;
                var trans = content.transform;
                trans.SetParent(targetCorner.transform);
            }

            ReloadCorner();
            targetCorner.ReloadCorner();
        }

        // 必要であれば現在のCornerの要素を下のCornerへと戻す
        public void BackContentsIfNeed()
        {
            if (originCorner != null)
            {
                if (originCorner.TryBorrowContents(this, true))
                {
                    originCorner = null;
                }
            }
        }

        // prefabを元にしたSwap
        // TODO: これはまるっとinstantiateすれば済むかもな~とも思った。これをどこかから実行する方が楽ぽい？
        public void InstantiateContentsFromPrefab(GameObject prefab)
        {
            // TODO: prefabじゃない場合になんとかする必要がある。WIP。
        }

        private List<Type> excludeTypes = new List<Type>();

        // 特定の型のContentをこのCornerの要素から除外する
        public void ExcludeContents<T>() where T : UIBehaviour
        {
            var excludeType = typeof(T);
            if (excludeTypes.Contains(excludeType))
            {
                // 既に含んでいるので何もしない
                return;
            }

            excludeTypes.Add(excludeType);
            ReloadContainedComponent();
        }

        // 特定のCorner型をこのCornerの要素から除外する
        public void ExcludeCorners<T>() where T : Corner
        {
            var excludeType = typeof(T);
            if (excludeTypes.Contains(excludeType))
            {
                // 既に含んでいるので何もしない
                return;
            }

            excludeTypes.Add(excludeType);
            ReloadContainedComponent();
        }

        // 現在除外設定されている型の一覧を返す
        public Type[] ExcludingTypes()
        {
            return excludeTypes.ToArray();
        }

        private List<GameObject> excludeObjects = new List<GameObject>();

        // 特定のコンポーネント群をこのCornerから明示的に除外する
        public void ExcludeContents<T>(T[] excludeTargetComponents) where T : UIBehaviour
        {
            foreach (var excludeTargetComponent in excludeTargetComponents)
            {
                var excludeGO = excludeTargetComponent.gameObject;
                if (excludeObjects.Contains(excludeGO))
                {
                    // 既に含んでいるので何もしない
                    continue;
                }

                excludeObjects.Add(excludeGO);
            }

            // まとめて一度だけ実行する。
            ReloadContainedComponent();
        }

        // 特定のコンポーネントをこのCornerから明示的に除外する
        public void ExcludeContent<T>(T excludeTargetComponent) where T : UIBehaviour
        {
            var excludeGO = excludeTargetComponent.gameObject;
            if (excludeObjects.Contains(excludeGO))
            {
                // 既に含んでいるので何もしない
                return;
            }

            excludeObjects.Add(excludeGO);
            ReloadContainedComponent();
        }

        // 現在除外設定されているGameObjectの一覧を返す
        public GameObject[] ExcludingGameObjects()
        {
            return excludeObjects.ToArray();
        }


        // Cornerが保持しているコンテンツ一覧を返す
        public RectTransform[] ExposureAllContents()
        {
            ReloadContainedComponent();
            return containedUIComponents;
        }

        // Cornerが保持しているコンテンツから、特定のUI型がついているUI Componentの集合を取得する。
        public bool TryExposureContents<T>(out T[] contents) where T : UIBehaviour
        {
            var rectTransformsList = new List<T>();

            ReloadContainedComponent();
            foreach (var containedUIComponent in containedUIComponents)
            {
                if (containedUIComponent.TryGetComponent<T>(out var component))
                {
                    rectTransformsList.Add(component);
                }
            }

            if (rectTransformsList.Any())
            {
                contents = rectTransformsList.ToArray();
                return true;
            }

            contents = null;
            return false;
        }

        public bool TryExposureCorners<T>(out T[] corners) where T : Corner
        {
            var cornerList = new List<T>();

            ReloadContainedComponent();
            foreach (var containedUIComponent in containedUIComponents)
            {
                if (containedUIComponent.TryGetComponent<T>(out var component))
                {
                    cornerList.Add(component);
                }
            }

            if (cornerList.Any())
            {
                corners = cornerList.ToArray();
                return true;
            }

            corners = null;
            return false;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GamenChangerCore
{
    /*
        基底のコーナー、単なる集合の箱。
        コーナーの内容を挿げ替える -> できた
        フリック可能にしてみる -> できた
        TODO: OneOfNを作ってみる
    */
    public class Corner : MonoBehaviour
    {
        [HideInInspector] public RectTransform[] containedUIComponents;

        [HideInInspector] public RectTransform currentRectTransform;

        // 生成時にコンテンツを収集する。
        public void Awake()
        {
            // UIを収集する
            var childCount = transform.childCount;
            var childUIComponent = new List<RectTransform>();

            // UIBehaviourの性質を持つコンポーネントのRectTransformを集める。
            for (var i = 0; i < childCount; i++)
            {
                var trans = transform.GetChild(i);
                var uiComponent = trans.GetComponent<UIBehaviour>();
                if (uiComponent == null)
                {
                    Debug.Log("not UI content found:" + uiComponent);
                    continue;
                }

                childUIComponent.Add(uiComponent.GetComponent<RectTransform>());
            }

            containedUIComponents = childUIComponent.ToArray();

            currentRectTransform = gameObject.GetComponent<RectTransform>();
        }

        // 内容物を書き換える
        // TODO: これを使ったタブ型のサンプルを作ろう。
        public void SwapContents(Corner corner)
        {
            // 中身を消す
            var childCount = transform.childCount;
            for (var i = 0; i < childCount; i++)
            {
                var trans = transform.GetChild(i);
                GameObject.Destroy(trans.gameObject);
            }

            // cornerの親が持っているUI要素を全て移し変える。swap元となったcornerは空になる。
            var contents = corner.ExposureContents();

            // ここで「親の位置」 の差に基づいて、全ての子の位置をずらす。
            var targetParentPos = corner.currentRectTransform;
            var diff = currentRectTransform.anchoredPosition - targetParentPos.anchoredPosition;

            foreach (var content in contents)
            {
                content.anchoredPosition += diff;
                var trans = content.transform;
                trans.SetParent(this.transform);
            }
        }

        // prefabを元にしたSwap
        // TODO: これはまるっとinstantiateすれば済むかもな~とも思った。これをどこかから実行する方が楽ぽい？
        public void SwapContentsFromPrefab(GameObject prefab)
        {

        }

        // コンテンツを露出させる
        public RectTransform[] ExposureContents()
        {
            return containedUIComponents;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using GamenChangerCore;
using UnityEngine;

public class SampleCode : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        // 適当なUIを展開するコードを書く。
        // TODO: Editor側のmenuItemかデバッグメソッドでQRコードが生成される。


        var fromCorner = GameObject.Find("FromCorner").GetComponent<Corner>();
        var toCorner = GameObject.Find("ToCorner").GetComponent<Corner>();
        fromCorner.SwapContents(toCorner);




    }
}

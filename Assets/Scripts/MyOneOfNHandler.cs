using System;
using GamenChangerCore;
using UnityEngine;
using UnityEngine.UI;

public class MyOneOfNHandler : MonoBehaviour, IOneOfNCornerHandler
{
    // 表示するコーナーと表示内容を保持しておくコーナーを複数持つ
    public Corner corner;
    public Corner corner1st;
    public Corner corner2nd;
    public Corner corner3rd;

    public Type TreatType()
    {
        return typeof(Button);
    }

    // TODO: ここの型を制約したい訳だが！！ MonoBehaviour型で渡ってきて、castできる、、とかだと嬉しいなあ。typeもパラメータで出してくれれば、、
    public void OnInitialized(GameObject one, GameObject[] all)
    {
        OnChangedToOne(one, all);
    }

    public void OnChangedToOne(GameObject one, GameObject[] all)
    {
        Debug.Log("OnChangedToOne:" + one + " cornerの内容をbutton textとかに合わせて変える");
        switch (one.name)
        {
            case "Button1":
                corner.BackContentsIfNeed();
                corner.TryBorrowContents(corner1st);
                break;
            case "Button2":
                corner.BackContentsIfNeed();
                corner.TryBorrowContents(corner2nd);
                break;
            case "Button3":
                corner.BackContentsIfNeed();
                corner.TryBorrowContents(corner3rd);
                break;
            default:
                Debug.LogError("unhandled name:" + one.gameObject.name);
                break;
        }
    }
}
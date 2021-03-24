using GamenChangerCore;
using UnityEngine;

public class MyOneOfNHandler : MonoBehaviour, IOneOfNCornerHandler
{
    public Corner corner;
    public Corner corner1st;
    public Corner corner2nd;
    public Corner corner3rd;

    public void OnInitialized(GameObject one)
    {
        OnChangedToOne(one);
    }

    public void OnChangedToOne(GameObject one)
    {
        Debug.Log("OnChangedToOne:" + one + " cornerの内容をbutton textとかに合わせて変える");
        switch (one.gameObject.name)
        {
            case "Button1":
                corner.BackContents();
                corner.BorrowContents(corner1st);
                break;
            case "Button2":
                corner.BackContents();
                corner.BorrowContents(corner2nd);
                break;
            case "Button3":
                corner.BackContents();
                corner.BorrowContents(corner3rd);
                break;
            default:
                Debug.LogError("unhandled name:" + one.gameObject.name);
                break;
        }
    }
}
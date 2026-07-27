using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CoinCount : MonoBehaviour
{

    public int Num;
    public Text legacyText;
    public Text Win;

    private void Start()
    {
        Win.text = "";

    }

    private void Update()
    {
        legacyText.text = $"{Num}";

        if (Num == 8)
        {
            Win.text = "Ты все собрал!";
        }
    }

}

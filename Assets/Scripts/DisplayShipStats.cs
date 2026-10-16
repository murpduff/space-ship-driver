using TMPro;
using UnityEngine;

public class DisplayShipStats : MonoBehaviour {
    [SerializeField] int floatPrecision;
    TextMeshProUGUI[] statText;
    string[] constantText;
    ShipMovement shipMovement;
    void Start() {
        statText = GetComponentsInChildren<TextMeshProUGUI>();
        constantText = new string[statText.Length];
        for(int i = 0; i < statText.Length; i++) {
            constantText[i] = statText[i].text;
        }

        shipMovement = FindAnyObjectByType<ShipMovement>();
    }

    void Update() {
        DisplayText(RoundFloat(shipMovement.rb.linearVelocity.magnitude).ToString(), 0);
        DisplayText(RoundFloat(shipMovement.acceleration.magnitude).ToString(), 1);
        DisplayText(shipMovement.stabilization.ToString(), 2, shipMovement.stabilization);
        DisplayText(shipMovement.setDirToTarget.ToString(), 3, shipMovement.setDirToTarget);
    }

    void DisplayText(string display, int i) {
        statText[i].text = constantText[i] + display;
    }

    void DisplayText(string display, int i, bool colorIndicator) {
        statText[i].text = constantText[i] + display;
        if(colorIndicator){
            statText[i].color = Color.green;
        }else {
            statText[i].color = Color.red;
        }
    }

    float RoundFloat(float value) {
        return Mathf.Round(value * Mathf.Pow(10, floatPrecision)) / Mathf.Pow(10, floatPrecision);
    }
}

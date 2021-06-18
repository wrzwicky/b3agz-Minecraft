using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using UnityEngine.SceneManagement;


public class TitleMenu : MonoBehaviour
{

    public GameObject mainMenuObject;
    public GameObject settingsObject;

    [Header("Main Menu UI Elements")]
    public TextMeshProUGUI seedField;

    [Header("Settings Menu UI Elements")]
    public Slider viewDistSlider;
    public TextMeshProUGUI viewDistText;
    public Slider mouseSensSlider;
    public TextMeshProUGUI mouseTextSlider;
    public Toggle threadingToggle;
    public Toggle chunkAnimToggle;
    public TMP_Dropdown cloudStyle;

    string spath;
    Settings settings;


    private void Start() {

        mainMenuObject.SetActive(true);
        settingsObject.SetActive(false);

    }

    private void Awake() {
        
        spath = Application.dataPath + "/settings.cfg";

        if(File.Exists(spath)) {

            Debug.Log("Settings file found, loading from " + spath);
            settings = Settings.LoadFile(spath);

        }
        else {

            Debug.Log("No settings file found, creating new one at " + spath);
            settings = new Settings();
            settings.SaveFile(spath);

        }

    }

    public void StartGame() {

        VoxelData.seed = Mathf.Abs(seedField.text.GetHashCode()) / VoxelData.WorldSizeInChunks;
        SceneManager.LoadScene("GameScene", LoadSceneMode.Single);

    }

    public void EnterSettings() {

        viewDistSlider.value = settings.viewDistance;
        viewDistText.text = "View Distance: " + viewDistSlider.value;

        mouseSensSlider.value = settings.mouseSensitivity * 10;
        mouseTextSlider.text = "Mouse Sensitivity: " + Mathf.Round(mouseSensSlider.value) / 10f;

        cloudStyle.value = ((int)settings.clouds);

        threadingToggle.isOn = settings.enableThreading;
        chunkAnimToggle.isOn = settings.enableAnimatedChunks;

        mainMenuObject.SetActive(false);
        settingsObject.SetActive(true);

    }

    public void LeaveSettings() {

        settings.viewDistance = (int)viewDistSlider.value;
        settings.mouseSensitivity = mouseSensSlider.value / 10f;
        settings.clouds = (CloudStyle)cloudStyle.value;
        settings.enableThreading = threadingToggle.isOn;
        settings.enableAnimatedChunks = chunkAnimToggle.isOn;

        settings.SaveFile(spath);

        mainMenuObject.SetActive(true);
        settingsObject.SetActive(false);

    }

    public void QuitGame() {

        Application.Quit();

    }

    public void UpdateViewDistSlider() {

        viewDistText.text = "View Distance: " + viewDistSlider.value;
        
    }

    public void UpdateMouseSensSlider() {

        mouseTextSlider.text = "Mouse Sensitivity: " + (mouseSensSlider.value / 10f).ToString("F1");

    }
}

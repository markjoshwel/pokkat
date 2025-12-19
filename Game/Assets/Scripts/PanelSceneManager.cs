//
using System.IO.Compression;
using UnityEngine;
using Firebase.Auth;

public class PanelSceneManager : MonoBehaviour
{
    public GameObject mainPage;
    public GameObject startPage;
    public GameObject logInPage;
    public GameObject signUpPage;
    public GameObject settingsPage;
    public GameObject navPage;
    public GameObject currentPage;
    //to assign all the panels for ease of making them active/inactive
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        mainPage.SetActive(true);
        currentPage == mainPage;
        startPage.SetActive(false);
        logInPage.SetActive(false);
        signUpPage.SetActive(false);
        settingsPage.SetActive(false);
        //making sure only main page is visible on bootup
    }
    public void ShowPanel()
    {
        if (navPage != null)
        {
            navPage.SetActive(true);
            currentPage.SetActive(false);
        }
    }

    public void navLogIn()
    {
        navPage == logInPage;
        ShowPanel();
        currentPage == logInPage;
    }

    public void navSignUp()
    {
        navPage == signUpPage;
        ShowPanel();
        currentPage == signUpPage;
    }
}

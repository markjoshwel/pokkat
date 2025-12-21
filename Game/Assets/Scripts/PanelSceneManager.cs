/*
 * author: arwen
 * date: 19/12/2025
 * description: manages what ui is shown
 */

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
    
    /// <summary>
    ///     to assign all the panels for ease of making them active/inactive
    /// </summary>
    private void Start()
    {
        mainPage.SetActive(true);
        startPage.SetActive(false);
        logInPage.SetActive(false);
        signUpPage.SetActive(false);
        settingsPage.SetActive(false);
        currentPage = mainPage;
        
        // making sure only main page is visible on bootup
    }

    /// <summary>
    ///     switch from current page to nav page
    /// </summary>
    private void ShowPanel()
    {
        if (navPage != null)
        {
            // hide current
            currentPage.SetActive(false);
            
            // switch
            currentPage = navPage;
            
            // show new/nav
            currentPage.SetActive(true);
        }
    }

    public void NavLogIn()
    {
        navPage = logInPage;
        ShowPanel();
    }

    public void NavSignUp()
    {
        navPage = signUpPage;
        ShowPanel();
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ScreenManager : MonoBehaviour
{
    public static ScreenManager Instance { get; private set; }

    [SerializeField] private Transform _screensContainer;
    [SerializeField] private ScreenBase _initialScreen;
    [SerializeField] private float _screenTransitionDuration = 0.5f;
    [SerializeField] private float _activityTimer = 60f;
    [SerializeField] private float _currentTimer = 0f;
    private Dictionary<Type, ScreenBase> _screens = new Dictionary<Type, ScreenBase>();
    private ScreenBase _currentScreen;
    private ScreenBase _previousScreen;
    private bool _isTransitioning;
    private bool _canCount;
    [SerializeField] private GameObject _networkError;
    [SerializeField] private Loader _loader;

    public GlobalChosesDataContainer container;


    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        container.Init();
        InitializeScreens();
    }

    public void StartScreens()
    {
        if (_initialScreen != null)
        {
            if (_initialScreen.GetType() == typeof(ScreensaverScreen))
            {
                ShowScreen<ScreensaverScreen>();
            }
            else
            {
                ShowScreen<DataInputScreen>();
            }

        }
    }

    private void InitializeScreens()
    {
        DirectoryInfo d = new DirectoryInfo(Application.streamingAssetsPath);
        var files = d.GetFiles("*.txt");
        if (files == null)
        {
            throw new System.Exception("NO CONFIGS IN STREAMING ASSETS");
        }
        try
        {
            var lightAppCongig = files.First((x) => x.Name.Contains("activityTimer"));
            var sr = lightAppCongig.OpenText();
            _activityTimer = float.Parse(sr.ReadToEnd());
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
        }


        foreach (Transform child in _screensContainer)
        {
            var screen = child.GetComponent<ScreenBase>();
            if (screen != null)
            {
                _screens.Add(screen.GetType(), screen);
                screen.Initialize();
                screen.gameObject.SetActive(false);
            }
        }
        InitErrors();
    }

    private void InitErrors()
    {
        _networkError.GetComponent<Button>().onClick.AddListener(DisableError);
        DisableError();
    }

    public T GetScreen<T>() where T : ScreenBase
    {

        if (_screens.TryGetValue(typeof(T), out ScreenBase screen))
        {
            return (T)screen;
        }
        return null;
    }

    private ScreenBase _transitionTarget;
    private ScreenBase _pendingScreen;
    private Action _pendingCompletion;

    public void ShowScreen<T>(Action onComplete = null) where T : ScreenBase
    {
        if (_screens.TryGetValue(typeof(T), out var screen)) RequestScreen(screen, onComplete);
        else Debug.LogError($"Screen of type {typeof(T)} not found!");
    }

    private void RequestScreen(ScreenBase screen, Action onComplete)
    {
        if (_isTransitioning)
        {
            _pendingScreen = screen == _transitionTarget ? null : screen;
            _pendingCompletion = _pendingScreen == null ? null : onComplete;
            return;
        }
        StartCoroutine(TransitionToScreen(screen, onComplete));
    }

    private IEnumerator TransitionToScreen(ScreenBase newScreen, Action onComplete)
    {
        _isTransitioning = true;
        try
        {
            while (newScreen != null)
            {
                _transitionTarget = newScreen;
                _canCount = _initialScreen == null || newScreen.GetType() != _initialScreen.GetType();
                _currentTimer = 0;
                if (_currentScreen != null)
                {
                    _previousScreen = _currentScreen;
                    _currentScreen.SetInputEnabled(false);
                    yield return Guarded(_currentScreen.AnimateHide());
                    _currentScreen.gameObject.SetActive(false);
                }
                _currentScreen = newScreen;
                _currentScreen.gameObject.SetActive(true);
                _currentScreen.SetInputEnabled(_currentScreen.InteractableWhileShowing);
                yield return Guarded(_currentScreen.AnimateShow());
                _currentScreen.SetInputEnabled(true);
                try { onComplete?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
                newScreen = _pendingScreen;
                onComplete = _pendingCompletion;
                _pendingScreen = null;
                _pendingCompletion = null;
            }
        }
        finally
        {
            _isTransitioning = false;
            _transitionTarget = null;
            if (_currentScreen != null) _currentScreen.SetInputEnabled(true);
        }
    }

    private static IEnumerator Guarded(IEnumerator routine)
    {
        try
        {
            while (true)
            {
                object current = null;
                bool hasNext;
                try
                {
                    hasNext = routine.MoveNext();
                    if (hasNext) current = routine.Current;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    hasNext = false;
                }
                if (!hasNext) yield break;
                if (current is IEnumerator nested) yield return Guarded(nested);
                else yield return current;
            }
        }
        finally { (routine as IDisposable)?.Dispose(); }
    }

    public void ShowPreviousScreen()
    {
        if (_previousScreen != null) RequestScreen(_previousScreen, null);
    }
    public void EnableError()
    {
        _networkError.SetActive(true);
    }

    public void DisableError()
    {
        _networkError.SetActive(false);
    }
    void Update()
    {
        if (_canCount)
        {
            _currentTimer += Time.deltaTime;
        }
        if (_currentTimer >= _activityTimer)
        {
            ResetSession();
        }
        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            _currentTimer = 0f;
        }
    }

    public void ResetSession()
    {
        _currentTimer = 0;
        _loader.CleanFolder();
        if (_initialScreen != null && _initialScreen.GetType() != typeof(ScreensaverScreen))
        {
            GetScreen<PhotoReviewScreen>()?.CleanPhotosContainer();
            container.Clean();
        }
        StartScreens();
        _loader.StartMonitoring();
    }
}

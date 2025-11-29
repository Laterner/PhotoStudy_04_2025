using UnityEngine;
using UnityEngine.UI;

public class PageList : MonoBehaviour
{
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Button scrollLeftButton;
    [SerializeField] private Button scrollRightButton;

    private void Start()
    {
        // Подписываемся на события кнопок
        scrollLeftButton.onClick.AddListener(ScrollToLeft);
        scrollRightButton.onClick.AddListener(ScrollToRight);
    }

    public void ScrollToLeft()
    {
        // Прокрутка в самое лево (horizontalNormalizedPosition = 0)
        scrollRect.horizontalNormalizedPosition = 0f;
    }

    public void ScrollToRight()
    {
        // Прокрутка в самое право (horizontalNormalizedPosition = 1)
        scrollRect.horizontalNormalizedPosition = 1f;
    }
}

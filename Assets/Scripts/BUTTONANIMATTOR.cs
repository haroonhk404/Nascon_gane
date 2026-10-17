using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class ButtonAnimator : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    [Header("References")]
    public Image hoverImage;            // child image with Button_Hover
    public TMP_Text label;              // button text (auto-found if empty)

    [Header("Entrance")]
    public float entranceDelay = 1.6f;  // wait for the logo to land
    public float slideDistance = 400f;  // starts this far to the left
    public float entranceTime = 0.6f;

    [Header("Idle glow")]
    public float idleGlowMin = 0f;
    public float idleGlowMax = 0.25f;
    public float idleGlowTime = 1.4f;

    [Header("Hover / Click")]
    public float hoverScale = 1.08f;
    public float pressScale = 0.94f;
    public float fadeTime = 0.25f;
    public Color normalTextColor = new Color32(255, 231, 184, 255);
    public Color hoverTextColor  = new Color32(255, 250, 230, 255);

    [Header("Sound (optional)")]
    public AudioSource audioSource;
    public AudioClip hoverSound;
    public AudioClip clickSound;

    RectTransform rect;
    CanvasGroup group;
    Vector2 homePos;
    Vector3 baseScale;
    bool ready;      // true after the entrance finishes
    bool hovered;
    Tween idleTween;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        group = GetComponent<CanvasGroup>();
        homePos = rect.anchoredPosition;
        baseScale = rect.localScale;
        if (label == null) label = GetComponentInChildren<TMP_Text>();
    }

    void OnEnable()
    {
        ready = false;
        hovered = false;

        rect.anchoredPosition = homePos - new Vector2(slideDistance, 0);
        rect.localScale = baseScale;
        group.alpha = 0f;
        group.interactable = false;
        SetHoverAlpha(0f);
        if (label) label.color = normalTextColor;

        DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .AppendInterval(entranceDelay)
            .Append(rect.DOAnchorPos(homePos, entranceTime).SetEase(Ease.OutBack))
            .Join(group.DOFade(1f, entranceTime * 0.7f))
            .OnComplete(() =>
            {
                ready = true;
                group.interactable = true;
                StartIdleGlow();
            });
    }

    // ---------- idle ----------
    void StartIdleGlow()
    {
        if (hoverImage == null || hovered) return;
        idleTween?.Kill();
        SetHoverAlpha(idleGlowMin);
        idleTween = hoverImage.DOFade(idleGlowMax, idleGlowTime)
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true).SetLink(gameObject);
    }

    void SetHoverAlpha(float a)
    {
        if (hoverImage == null) return;
        var c = hoverImage.color; c.a = a; hoverImage.color = c;
    }

    // ---------- hover ----------
    void Enter()
    {
        if (!ready) return;
        hovered = true;

        idleTween?.Kill();
        if (hoverImage) { hoverImage.DOKill(); hoverImage.DOFade(1f, fadeTime).SetUpdate(true); }

        rect.DOKill();
        rect.DOScale(baseScale * hoverScale, fadeTime).SetEase(Ease.OutBack).SetUpdate(true);
        rect.DOPunchRotation(new Vector3(0, 0, 1.5f), 0.35f, 8, 0.5f).SetUpdate(true);

        if (label) { label.DOKill(); label.DOColor(hoverTextColor, fadeTime).SetUpdate(true); }
        if (audioSource && hoverSound) audioSource.PlayOneShot(hoverSound);
    }

    void Exit()
    {
        if (!ready) return;
        hovered = false;

        rect.DOKill();
        rect.localRotation = Quaternion.identity;
        rect.DOScale(baseScale, fadeTime).SetEase(Ease.OutQuad).SetUpdate(true);

        if (label) { label.DOKill(); label.DOColor(normalTextColor, fadeTime).SetUpdate(true); }
        if (hoverImage)
        {
            hoverImage.DOKill();
            hoverImage.DOFade(idleGlowMin, fadeTime).SetUpdate(true).OnComplete(StartIdleGlow);
        }
    }

    public void OnPointerEnter(PointerEventData e) => Enter();
    public void OnPointerExit(PointerEventData e)  => Exit();
    public void OnSelect(BaseEventData e)          => Enter();
    public void OnDeselect(BaseEventData e)        => Exit();

    // ---------- click ----------
    public void OnPointerDown(PointerEventData e)
    {
        if (!ready) return;
        rect.DOKill();
        rect.DOScale(baseScale * pressScale, 0.08f).SetUpdate(true);
        if (audioSource && clickSound) audioSource.PlayOneShot(clickSound);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!ready) return;
        rect.DOScale(baseScale * (hovered ? hoverScale : 1f), 0.18f)
            .SetEase(Ease.OutBack).SetUpdate(true);
    }

    void OnDisable()
    {
        idleTween?.Kill();
        rect.DOKill();
        group.DOKill();
        if (hoverImage) hoverImage.DOKill();
        if (label) label.DOKill();

        rect.anchoredPosition = homePos;
        rect.localScale = baseScale;
        rect.localRotation = Quaternion.identity;
        group.alpha = 1f;
    }
}
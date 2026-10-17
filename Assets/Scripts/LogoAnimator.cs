using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(CanvasGroup))]
public class LogoAnimator : MonoBehaviour
{
    [Header("Entrance")]
    public float startDelay = 0.3f;
    public float dropDistance = 250f;
    public float entranceTime = 0.9f;

    [Header("Idle loop")]
    public float floatAmount = 10f;
    public float floatTime = 2f;
    public float pulseScale = 1.04f;   // relative: 1.04 = 4% bigger
    public float pulseTime = 1.6f;
    public float wobbleAngle = 1.5f;
    public float wobbleTime = 2.6f;

    RectTransform rect;
    CanvasGroup group;
    Vector2 homePos;
    Vector3 baseScale;     // remembers YOUR scale
    Sequence entrance;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        group = GetComponent<CanvasGroup>();
        homePos = rect.anchoredPosition;
        baseScale = rect.localScale;
    }

    void OnEnable()
    {
        rect.anchoredPosition = homePos + new Vector2(0, dropDistance);
        rect.localScale = baseScale * 0.6f;
        rect.localRotation = Quaternion.Euler(0, 0, -8f);
        group.alpha = 0f;

        entrance = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        entrance.AppendInterval(startDelay);
        entrance.Append(rect.DOAnchorPos(homePos, entranceTime).SetEase(Ease.OutBounce));
        entrance.Join(group.DOFade(1f, entranceTime * 0.5f));
        entrance.Join(rect.DOScale(baseScale, entranceTime).SetEase(Ease.OutBack));
        entrance.Join(rect.DOLocalRotate(Vector3.zero, entranceTime).SetEase(Ease.OutBack));
        entrance.Append(rect.DOPunchScale(baseScale * 0.12f, 0.4f, 6, 0.6f));
        entrance.OnComplete(StartIdle);
    }

    void StartIdle()
    {
        rect.localScale = baseScale;

        rect.DOAnchorPosY(homePos.y + floatAmount, floatTime)
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true).SetLink(gameObject);

        rect.DOScale(baseScale * pulseScale, pulseTime)
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true).SetLink(gameObject);

        rect.localRotation = Quaternion.Euler(0, 0, -wobbleAngle);
        rect.DOLocalRotate(new Vector3(0, 0, wobbleAngle), wobbleTime)
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true).SetLink(gameObject);
    }

    void OnDisable()
    {
        entrance?.Kill();
        rect.DOKill();
        group.DOKill();
        rect.anchoredPosition = homePos;
        rect.localScale = baseScale;
        rect.localRotation = Quaternion.identity;
        group.alpha = 1f;
    }
}
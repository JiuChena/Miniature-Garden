using System.Collections.Generic;
using CoreFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GamingPanel : PanelBase
{
    private const int MaxTeamSlots = 3;
    [Tooltip("当前角色主头像")] public Image currentCharacterIcon;
    [Tooltip("当前角色天赋图标")] public Image currentCharacterTalentIcon;
    [Tooltip("当前角色爆发图标")] public Image currentCharacterBurstIcon;
    [Tooltip("天赋冷却Mask")] public Image currentCharacterTalentCooldownMask;
    [Tooltip("爆发冷却Mask")] public Image currentCharacterBurstCooldownMask;
    [Tooltip("当前角色武器图标")] public Image currentCharacterWeaponIcon;
    [Tooltip("当前角色主血条")] public Image currentCharacterHealth;
    [Tooltip("当前角色主血条文本")] public TMP_Text currentCharacterHealthText;
    [Tooltip("编队角色项1")] public GameObject teamCharacterItem1;
    [Tooltip("编队角色项2")] public GameObject teamCharacterItem2;
    [Tooltip("编队角色项3")] public GameObject teamCharacterItem3;
    [Tooltip("编队角色激活图标1")] public Image teamCharacterActived1;
    [Tooltip("编队角色激活图标2")] public Image teamCharacterActived2;
    [Tooltip("编队角色激活图标3")] public Image teamCharacterActived3;
    [Tooltip("编队角色头像1")] public Image teamCharacterIcon1;
    [Tooltip("编队角色头像2")] public Image teamCharacterIcon2;
    [Tooltip("编队角色头像3")] public Image teamCharacterIcon3;
    [Tooltip("编队角色名称1")] public TMP_Text teamCharacterName1;
    [Tooltip("编队角色名称2")] public TMP_Text teamCharacterName2;
    [Tooltip("编队角色名称3")] public TMP_Text teamCharacterName3;
    [Tooltip("编队角色血条1")] public Image teamCharacterHealth1;
    [Tooltip("编队角色血条2")] public Image teamCharacterHealth2;
    [Tooltip("编队角色血条3")] public Image teamCharacterHealth3;
    [Space(10)]
    [Tooltip("灰度材质")] public Material grayMaterial;
    
    Queue<KeyValuePair<int, int>> pos = new();
    
    private GameObject[] _teamItems;
    private Image[] _teamActivedImages;
    private Image[] _teamIcons;
    private TMP_Text[] _teamNames;
    private Image[] _teamHealthBars;
    private CharacterDriver _cachedCurrentCharacter;
    private int _cachedCurrentCharacterIndex = -1;
    private int _cachedConfiguredCharacterCount = -1;
    private readonly CharacterDriver[] _cachedPartyCharacters = new CharacterDriver[MaxTeamSlots];
    private bool _switchEventSubscribed;
    private float _currentCharacterHealthMaxWidth = -1f;

    protected override void EventInit()
    {
        TypedEventBus.Subscribe<PlayerCharacterSwitchedEvent>(HandlePlayerCharacterSwitched);
        _switchEventSubscribed = true;
    }

    protected override void ComponentInit()
    {
        _teamItems = new[] { teamCharacterItem1, teamCharacterItem2, teamCharacterItem3 };
        _teamActivedImages = new[] { teamCharacterActived1, teamCharacterActived2, teamCharacterActived3 };
        _teamIcons = new[] { teamCharacterIcon1, teamCharacterIcon2, teamCharacterIcon3 };
        _teamNames = new[] { teamCharacterName1, teamCharacterName2, teamCharacterName3 };
        _teamHealthBars = new[] { teamCharacterHealth1, teamCharacterHealth2, teamCharacterHealth3 };
        CacheCurrentCharacterHealthWidth();
        InitializePartyState(PlayerController.Instance);
        
        animator.SetTrigger("Open");
    }

    protected override void OnUpdate()
    {
        RefreshAll();
    }

    private void OnDestroy()
    {
        if (!_switchEventSubscribed)
            return;
        TypedEventBus.Unsubscribe<PlayerCharacterSwitchedEvent>(HandlePlayerCharacterSwitched);
        _switchEventSubscribed = false;
    }

    private void HandlePlayerCharacterSwitched(PlayerCharacterSwitchedEvent eventData)
    {
        if (eventData.Player != PlayerController.Instance)
            return;
        RefreshCurrentCharacterDisplay(eventData.CurrentCharacter);
        RefreshTeamActivedState(eventData.CurrentCharacterIndex);
        RefreshCurrentCharacterHealthBar(eventData.CurrentCharacter);
        RefreshTeamHealthBars(eventData.Player);
    }

    private void RefreshAll()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null)
        {
            InitializePartyState(null);
            return;
        }
        if (HasPartyChanged(player))
            InitializePartyState(player);
        CharacterDriver currentCharacter = player.CurrentCharacter;
        if (_cachedCurrentCharacter != currentCharacter || _cachedCurrentCharacterIndex != player.CurrentCharacterIndex)
        {
            RefreshCurrentCharacterDisplay(currentCharacter);
            RefreshTeamActivedState(player.CurrentCharacterIndex);
        }
        else
        {
            RefreshCurrentCooldownMasks(currentCharacter);
        }
        RefreshCurrentCharacterHealthBar(currentCharacter);
        RefreshTeamHealthBars(player);
    }

    private void InitializePartyState(PlayerController player)
    {
        CachePartyState(player);
        CacheCurrentCharacterHealthWidth();
        RefreshCurrentCharacterDisplay(player != null ? player.CurrentCharacter : null);
        RefreshTeamDisplay(player);
        RefreshTeamActivedState(player != null ? player.CurrentCharacterIndex : -1);
        RefreshCurrentCharacterHealthBar(player != null ? player.CurrentCharacter : null);
        RefreshTeamHealthBars(player);
    }

    private void CachePartyState(PlayerController player)
    {
        _cachedConfiguredCharacterCount = player != null ? player.ConfiguredCharacterCount : 0;
        for (int i = 0; i < MaxTeamSlots; i++)
            _cachedPartyCharacters[i] = ResolveTeamCharacter(player, i);
    }

    private void CacheCurrentCharacterHealthWidth()
    {
        if (currentCharacterHealth == null)
            return;
        RectTransform rectTransform = currentCharacterHealth.rectTransform;
        if (rectTransform == null)
            return;
        float width = rectTransform.rect.width;
        if (width <= 0f)
            width = rectTransform.sizeDelta.x;
        if (width > 0f)
            _currentCharacterHealthMaxWidth = width;
    }

    private bool HasPartyChanged(PlayerController player)
    {
        int configuredCount = player != null ? player.ConfiguredCharacterCount : 0;
        if (configuredCount != _cachedConfiguredCharacterCount)
            return true;
        for (int i = 0; i < MaxTeamSlots; i++)
        {
            if (_cachedPartyCharacters[i] != ResolveTeamCharacter(player, i))
                return true;
        }
        return false;
    }

    private void RefreshCurrentCharacterDisplay(CharacterDriver character)
    {
        _cachedCurrentCharacter = character;
        _cachedCurrentCharacterIndex = character != null && PlayerController.Instance != null ? PlayerController.Instance.CurrentCharacterIndex : -1;
        UnitAssetInformation config = character != null ? character.Config : null;
        ApplyImageSprite(currentCharacterIcon, config != null ? config.PortraitIcon : null);
        ApplyImageSprite(currentCharacterTalentIcon, config != null ? config.TalentIcon : null);
        ApplyImageSprite(currentCharacterBurstIcon, config != null ? config.BurstIcon : null);
        ApplyImageSprite(currentCharacterWeaponIcon, config != null ? config.WeaponIcon : null);
        ApplyDeathMaterial(currentCharacterIcon, IsCharacterDead(character), grayMaterial);
        RefreshCurrentCooldownMasks(character);
        RefreshCurrentCharacterHealthBar(character);
    }

    private void RefreshCurrentCooldownMasks(CharacterDriver character)
    {
        UnitAssetInformation config = character != null ? character.Config : null;
        CharacterCooldowns cooldowns = character != null && character.Context != null ? character.Context.Cooldowns : null;
        ApplyCooldownMask(currentCharacterTalentCooldownMask, cooldowns, "Talent", config != null ? config.TalentCooldown : 0f);
        ApplyCooldownMask(currentCharacterBurstCooldownMask, cooldowns, "Burst", config != null ? config.BurstCooldown : 0f);
    }

    private void RefreshTeamDisplay(PlayerController player)
    {
        for (int i = 0; i < MaxTeamSlots; i++)
        {
            CharacterDriver character = ResolveTeamCharacter(player, i);
            bool shouldShow = character != null;
            if (_teamItems != null && i < _teamItems.Length && _teamItems[i] != null)
                _teamItems[i].SetActive(shouldShow);
            if (!shouldShow)
            {
                ApplyActivedState(_teamActivedImages != null && i < _teamActivedImages.Length ? _teamActivedImages[i] : null, false);
                ApplyImageSprite(_teamIcons != null && i < _teamIcons.Length ? _teamIcons[i] : null, null);
                ApplyDeathMaterial(_teamIcons != null && i < _teamIcons.Length ? _teamIcons[i] : null, false, grayMaterial);
                ApplyText(_teamNames != null && i < _teamNames.Length ? _teamNames[i] : null, string.Empty);
                ApplyHealthFill(_teamHealthBars != null && i < _teamHealthBars.Length ? _teamHealthBars[i] : null, 0f);
                continue;
            }
            UnitAssetInformation config = character.Config;
            ApplyImageSprite(_teamIcons != null && i < _teamIcons.Length ? _teamIcons[i] : null, config != null ? config.TeamIcon : null);
            ApplyDeathMaterial(_teamIcons != null && i < _teamIcons.Length ? _teamIcons[i] : null, IsCharacterDead(character), grayMaterial);
            ApplyText(_teamNames != null && i < _teamNames.Length ? _teamNames[i] : null, ResolveCharacterDisplayName(character));
        }
    }

    private void RefreshTeamActivedState(int currentCharacterIndex)
    {
        for (int i = 0; i < MaxTeamSlots; i++)
            ApplyActivedState(_teamActivedImages != null && i < _teamActivedImages.Length ? _teamActivedImages[i] : null, i == currentCharacterIndex);
    }

    private void RefreshTeamHealthBars(PlayerController player)
    {
        for (int i = 0; i < MaxTeamSlots; i++)
        {
            CharacterDriver character = ResolveTeamCharacter(player, i);
            ApplyHealthFill(_teamHealthBars != null && i < _teamHealthBars.Length ? _teamHealthBars[i] : null, ResolveHealthFill(character));
            ApplyDeathMaterial(_teamIcons != null && i < _teamIcons.Length ? _teamIcons[i] : null, IsCharacterDead(character), grayMaterial);
        }
    }

    private void RefreshCurrentCharacterHealthBar(CharacterDriver character)
    {
        float fillAmount = ResolveHealthFill(character);
        ApplyHealthWidth(currentCharacterHealth, fillAmount, ref _currentCharacterHealthMaxWidth);
        ApplyText(currentCharacterHealthText, ResolveHealthText(character));
        ApplyDeathMaterial(currentCharacterIcon, IsCharacterDead(character), grayMaterial);
    }

    private static CharacterDriver ResolveTeamCharacter(PlayerController player, int slotIndex)
    {
        if (player == null || slotIndex < 0 || slotIndex >= player.ConfiguredCharacterCount)
            return null;
        System.Collections.Generic.IReadOnlyList<CharacterDriver> configuredCharacters = player.ConfiguredCharacters;
        if (configuredCharacters == null || slotIndex >= configuredCharacters.Count)
            return null;
        return configuredCharacters[slotIndex];
    }

    private static string ResolveCharacterDisplayName(CharacterDriver character)
    {
        if (character == null)
            return string.Empty;
        UnitAssetInformation config = character.Config;
        if (config != null)
            return config.DisplayName;
        return character.name;
    }

    private static float ResolveHealthFill(CharacterDriver character)
    {
        if (!TryResolveHealthValues(character, out float currentHealth, out float maxHealth, out bool isDead))
            return 0f;
        if (maxHealth <= 0f || isDead)
            return 0f;
        return Mathf.Clamp01(currentHealth / maxHealth);
    }

    private static string ResolveHealthText(CharacterDriver character)
    {
        if (!TryResolveHealthValues(character, out float currentHealth, out float maxHealth, out bool isDead))
            return "0 / 0";
        if (maxHealth <= 0f)
            return "0 / 0";
        int currentHealthValue = isDead ? 0 : Mathf.Max(0, Mathf.RoundToInt(currentHealth));
        int maxHealthValue = Mathf.Max(0, Mathf.RoundToInt(maxHealth));
        return $"{currentHealthValue} / {maxHealthValue}";
    }

    private static bool TryResolveHealthValues(CharacterDriver character, out float currentHealth, out float maxHealth,
        out bool isDead)
    {
        currentHealth = 0f;
        maxHealth = 0f;
        isDead = false;
        if (character == null)
            return false;
        StatusData statusData = character.DataPanel;
        if (statusData != null && statusData.MaxHealth > 0f)
        {
            maxHealth = statusData.MaxHealth;
            currentHealth = statusData.IsDead ? 0f : statusData.CurrentHealth;
            isDead = statusData.IsDead;
            return true;
        }
        UnitAssetInformation config = character.Config;
        if (config == null)
            return false;
        int characterLevel = ResolveCharacterLevel(config);
        maxHealth = Mathf.Max(0f, config.ResolveBaseHealth(characterLevel));
        currentHealth = maxHealth;
        return maxHealth > 0f;
    }

    private static int ResolveCharacterLevel(UnitAssetInformation config)
    {
        if (config == null || config.UnitId <= 0)
            return 1;
        CharacterData characterData = CharacterDataManager.Instance.GetCharacterDataOrDefault(config.UnitId);
        if (characterData == null)
            return 1;
        return Mathf.Max(1, characterData.characterLevel);
    }

    private static bool IsCharacterDead(CharacterDriver character)
    {
        return TryResolveHealthValues(character, out _, out _, out bool isDead) && isDead;
    }

    private static void ApplyCooldownMask(Image image, CharacterCooldowns cooldowns, string cooldownId, float totalDuration)
    {
        if (image == null)
            return;
        if (cooldowns == null || totalDuration <= 0f || !cooldowns.TryGetRemaining(cooldownId, out float remaining))
        {
            image.fillAmount = 0f;
            image.enabled = false;
            return;
        }
        image.fillAmount = Mathf.Clamp01(remaining / totalDuration);
        image.enabled = image.fillAmount > 0f;
    }

    private static void ApplyActivedState(Image image, bool isActived)
    {
        if (image == null)
            return;
        image.gameObject.SetActive(isActived);
    }

    private static void ApplyImageSprite(Image image, Sprite sprite)
    {
        if (image == null)
            return;
        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private static void ApplyDeathMaterial(Image image, bool isDead, Material grayMaterial)
    {
        if (image == null)
            return;
        image.material = isDead && grayMaterial != null ? grayMaterial : null;
    }

    private static void ApplyText(TMP_Text text, string content)
    {
        if (text == null)
            return;
        text.text = content ?? string.Empty;
    }

    private static void ApplyHealthFill(Image image, float fillAmount)
    {
        if (image == null)
            return;
        image.fillAmount = Mathf.Clamp01(fillAmount);
    }

    private static void ApplyHealthWidth(Image image, float fillAmount, ref float maxWidth)
    {
        if (image == null)
            return;
        float clampedFillAmount = Mathf.Clamp01(fillAmount);
        RectTransform rectTransform = image.rectTransform;
        if (rectTransform == null)
            return;
        if (maxWidth <= 0f)
        {
            float width = rectTransform.rect.width;
            if (width <= 0f)
                width = rectTransform.sizeDelta.x;
            if (width > 0f)
                maxWidth = width;
        }
        image.fillAmount = clampedFillAmount;
        if (maxWidth <= 0f)
            return;
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, maxWidth * clampedFillAmount);
    }
}

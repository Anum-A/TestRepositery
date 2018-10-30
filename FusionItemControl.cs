using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace Cloudcade
{
    public class FusionItemControl : ObjectControl
    {
        public class FusionInfo
        {
            public long index;
            public FusionControl.Mode mode;
            public InventoryItemProxy iip;
            public Action onChange;

            //Enchanting Stuff
            public int fusionQualityMaximum;
            public int fusionQualityMinimum;

            public bool isPremium;
        }

        public ButtonController button;
        public RectTransform itemContent;
        public ItemControl itemPrefab;
        public ItemControl itemControl { get; set; }
        public Image addImage;
        public Image arrowImage;
        public Image lockImage;
        public TextMeshProUGUI lockText;
        public TextMeshPro mythicText;
        public Image premiumJewel;

        public Image background;

        public Sprite buttonUp;
        public Sprite buttonDown;
        public Sprite buttonDisabled;
        public Sprite buttonItemSelected;

        private SpriteState setupSSUnselected;
        private SpriteState setupSSSelected;

        public Material desaturate;

        public Animation anim;
        public AnimationClip animFloatUp;
        public AnimationClip animFloatIn;
        public AnimationClip animFloatOut;

        public Animator animator;

        public Action<InventoryItemProxy> onItemSelected;
        public Action onItemUnselected;

        private long index
        {
            get
            {
                return (data as FusionInfo).index;
            }
        }

        private FusionControl.Mode mode
        {
            get
            {
                return (data as FusionInfo).mode;
            }
        }

        public InventoryItemProxy iip
        {
            get
            {
                return (data as FusionInfo).iip;
            }
            set
            {
                if(mode == FusionControl.Mode.Setup && !IsPremium)
                {
                    if (iip != null && value == null)
                    {
                        iip.Quantity++;
                    }

                    if (iip == null && value != null)
                    {
                        value.Quantity--;
                    }
                }
                (data as FusionInfo).iip = value;
                (data as FusionInfo).onChange.Fire();
                UpdateUI();
            }
        }

        public int FusionQualityMaximum
        {
            get
            {
                return (data as FusionInfo).fusionQualityMaximum;
            }
            set
            {
                (data as FusionInfo).fusionQualityMaximum = value;
            }
        }

        public int FusionQualityMinumum
        {
            get
            {
                return (data as FusionInfo).fusionQualityMinimum;
            }
            set
            {
                (data as FusionInfo).fusionQualityMinimum = value;
            }
        }

        public bool IsPremium
        {
            get
            {
                return (data as FusionInfo).isPremium;
            }
            set
            {
                (data as FusionInfo).isPremium = value;
            }
        }

        protected override void Awake()
        {
            base.Awake();

            setupSSUnselected = new SpriteState();
            setupSSUnselected.pressedSprite = buttonDown;
            setupSSUnselected.disabledSprite = buttonDisabled;

            setupSSSelected = new SpriteState();
            setupSSSelected.pressedSprite = buttonItemSelected;
            setupSSSelected.disabledSprite = buttonItemSelected;
        }

        protected override void Start()
        {
            base.Start();

            button.onClick.AddListener(OnSelection);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            button.onClick.RemoveListener(OnSelection);
        }

        protected override void OnUpdateUI()
        {
            if(data != null)
            {
                if(itemControl == null)
                {
                    itemControl = Control.Clone(itemPrefab);
                    itemControl.SetParent(itemContent);
                }
                itemControl.gameObject.SetActive(false);
                mythicText.text = string.Empty;
                if (iip != null)
                {
                    itemControl.gameObject.SetActive(true);
                    itemControl.data = iip;
                    if (iip.Quality > InventoryItem.QUALITY6)
                        mythicText.text = string.Format("+{0}", iip.Quality - InventoryItem.QUALITY6);
                }

                long fusionSlots = (long)Game.instance.user.Modifier.value["fusion_slots"];
                bool isLocked = index >= fusionSlots;
                lockText.text = isLocked ? Utils.FormatTitle(string.Format("{0} {1}", Game.instance.text.GetText("level"), index == 3 ? 6 : 11)) : string.Empty;
                lockImage.gameObject.SetActive(isLocked);
                if(mode == FusionControl.Mode.Setup)
                {   
                    button.interactable = !isLocked;
                    arrowImage.material = isLocked ? desaturate : null;

                    addImage.gameObject.SetActive(!isLocked && iip == null);

                    if(iip == null)
                    {
                        background.sprite = buttonUp;
                        button.spriteState = setupSSUnselected;
                    }
                    else
                    {
                        background.sprite = buttonItemSelected;
                        button.spriteState = setupSSSelected;
                    }
                }
                else
                {
                    addImage.gameObject.SetActive(false);
                    if(isLocked)
                    {
                        button.interactable = false;
                    }
                    else
                    {
                        button.button.enabled = false;
                        background.sprite = buttonItemSelected;
                    }
                    arrowImage.material = desaturate;
                }

                if (IsPremium)
                {
                    button.interactable = false;
                    background.sprite = buttonItemSelected;
                    button.spriteState = setupSSSelected;
                    addImage.gameObject.SetActive(false);
                    if (premiumJewel != null)
                        premiumJewel.enabled = true;
                }
                else
                {
                    if (premiumJewel != null)
                        premiumJewel.enabled = false;
                }
            }
        }

        private void OnSelection()
        {
            if(data != null)
            {
                if(iip == null)
                {
                    ViewInventory view = Game.instance.views.ViewCreate<ViewInventory>();
                    view.updateCardAfterSelection = true;
                    view.isFusion = true;
                    view.qualityMaximum = FusionQualityMaximum;
                    view.qualityMinimum = FusionQualityMinumum;
                    view.onInventoryItemProxySelected += OnItemSelected;
                    view.ViewShow();
                    Game.instance.soundManager.PlaySound(eSoundId.eSFX_CardListOpen);
                }
                else
                {
                    iip = null;
                    onItemUnselected.Fire();
                    Game.instance.soundManager.PlaySound(eSoundId.eSFX_GenericClick);
                }
            }
        }

        private void OnItemSelected(object data)
        {
            ViewInventory view = Game.instance.views.GetView<ViewInventory>();
            if (Game.instance.settingsManager.gameOptionToggles[SettingsManager.GameOption.Multicraft])
            {
                onItemSelected.Fire(data as InventoryItemProxy);
            }
            else
            {
                view.onInventoryItemProxySelected -= OnItemSelected;
                WindowManager.instance.RemoveCurrentFullScreenWindow();
                iip = data as InventoryItemProxy;
            }

            Game.instance.soundManager.PlaySound(eSoundId.eSFX_CharacterEquip);
        }

        public void UnregisterFromView()
        {
            ViewInventory view = Game.instance.views.GetView<ViewInventory>();
            view.onInventoryItemProxySelected -= OnItemSelected;
        }
    }
}

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


using System.Collections;
namespace Cloudcade
{
    public class FusionControl : Control
    {
        private const int ANIM_SPEEDUP_MULT = 4;
        public enum Mode
        {
            Setup,
            Completion
        }
        public Mode mode  { get { return slot.Type.value == null ? Mode.Setup : Mode.Completion; } }
        public Slot slot { get; set; }

        public TextMeshProUGUI bannerTitle;
        public ButtonController exitButton;
        public ButtonController clickCatcher;

        public TextMeshProUGUI resultTitle;
        public FusionItemProbControl[] mostProbableResults;

        public TextMeshProUGUI bestResultTitle;
        public FusionItemProbControl bestResult;

        public TextMeshProUGUI successRateTitle;
        public TextMeshProControl successRateText;

        public FusionItemControl[] fusionItems;
        public ImageLoader[] fusionStationImages;
        public SpriteRendererLoader[] fusionStationTopImages;

        public TextMeshProControl fusionSuccess;
        public Image fusionSuccessBorder;
        public Color fusionSuccessColor;
        public Color fusionFailedColor;

        public RectTransform fusedItemContent;
        public TextMeshPro fusedMythic;
        private ItemControl fusedItemControl;
        public ItemControl itemPrefab;

        public ButtonController startButton;
        public ButtonController clearButton;
        public ButtonController continueButton;
        public ButtonController shareButton;
        public ButtonToggleController mythicButton;
        public ButtonController upgradeButton;

        public Animation anim;
        public AnimationClip animStartPhase1;
        public AnimationClip animStartPhase2;

        public AnimationClip animEndPhase1;
        public AnimationClip animEndPhase2;
        public AnimationClip animEndPhase2Success;
        public AnimationClip animEndPhase2Failure;
        public AnimationClip animEndPhase3;

        public Transform splashTraget;

        public RectTransform successGlowPanel;

        public Button speedUpAnimClickCatcher;

        private Machine machine = new Machine("fusion_machine");

        public DarkTonic.MasterAudio.PlaySoundResult endSoundResult { get; set; }

        private bool _speedUpAnim;
        public bool speedUpAnim
        {
            get
            {
                return _speedUpAnim;
            }
            set
            {
                if(_speedUpAnim != value)
                {
                    _speedUpAnim = value;
                    anim[animEndPhase2Success.name].speed = _speedUpAnim ? ANIM_SPEEDUP_MULT : 1;
                    anim[animEndPhase2Failure.name].speed = _speedUpAnim ? ANIM_SPEEDUP_MULT : 1;
                    anim[animEndPhase1.name].speed = _speedUpAnim ? ANIM_SPEEDUP_MULT : 1;
                    anim[animEndPhase2.name].speed = _speedUpAnim ? ANIM_SPEEDUP_MULT : 1;
                    anim[animStartPhase1.name].speed = _speedUpAnim ? ANIM_SPEEDUP_MULT : 1;
                    anim[animStartPhase2.name].speed = _speedUpAnim ? ANIM_SPEEDUP_MULT : 1;
                    
                    foreach(FusionItemControl fusionItem in fusionItems)
                    {
                        fusionItem.anim[fusionItem.animFloatUp.name].speed = _speedUpAnim ? ANIM_SPEEDUP_MULT : 1;
                        fusionItem.anim[fusionItem.animFloatIn.name].speed = _speedUpAnim ? ANIM_SPEEDUP_MULT : 1;
                        fusionItem.anim[fusionItem.animFloatOut.name].speed = _speedUpAnim ? ANIM_SPEEDUP_MULT : 1;
                    }
                }
            }
        }

        private List<InventoryItemProxy> ingredients = new List<InventoryItemProxy>();
        private ServerCommand _command = new ServerCommand();

		public class InputInfo
		{
			public ItemData itemData;
			public int quality;

			public string uid{get{return itemData.uid;}}
			public int level{get{return itemData.level;}}

			public string key
			{
				get
				{
					return string.Format("{0}:{1}", uid, quality.ToString());
				}
			}
		}
		
		public class OutputInfo : InputInfo
		{
			public double weight;
			public double probability;
			public long price{get{return InventoryItem.FindItemPriceByQuality(itemData, quality);}}
		}

        public override bool CanGoBack()
        {
            if(slot == null)
                return true;
            return !machine.started && string.IsNullOrEmpty(slot.Type.value);
        }

        protected override void Start()
        {
            base.Start();

            bannerTitle.text = Game.instance.text.GetText("fusion_title");
            resultTitle.text = Game.instance.text.GetText("fusion_most_likely");
            bestResultTitle.text = Game.instance.text.GetText("fusion_best_value");
            successRateTitle.text = Game.instance.text.GetText("fusion_success_rate");

            startButton.gameObject.SetActive(mode == Mode.Setup);
			startButton.title = Game.instance.text.GetText("call_to_action_fuse");
            startButton.onClick.AddListener(StartFusion);

            exitButton.onClick.AddListener(OnCancelExit);
            clickCatcher.onClick.AddListener(OnCancelExit);
            clearButton.onClick.AddListener(GiveBackItems);
            shareButton.onClick.AddListener(OnShare);
            mythicButton.onToggled += OnMythic;
            upgradeButton.onClick.AddListener(OnUpgrade);
            speedUpAnimClickCatcher.onClick.AddListener(OnSpeedUpAnims);

            continueButton.onClick.AddListener(OnAcknowledgeResult);
            continueButton.text = Game.instance.text.GetText("continue");

            Module module = Game.instance.user.Modules.FirstOrDefault(x => x.Data.fusionMax > 0);
            if(module != null)
            {
                string imgName = module.GetAssetName();
                int assetLevel = Convert.ToInt32(Module.GetAssetLevel(module.Level) - 1);

                ImageLoader stationImg = fusionStationImages[assetLevel];
                stationImg.gameObject.SetActive(true);
                stationImg.LoadAsync(imgName);

                SpriteRendererLoader stationTopImg = fusionStationTopImages[assetLevel];
                stationTopImg.gameObject.SetActive(true);
                stationTopImg.LoadAsync(string.Format("{0}_part01", imgName));
            }

            InitIngredients();

			UpdateUI();

            if (slot.Type.value != null)
            {
                machine.TransitionTo(new FusionEndPhase1(this));

                fusedItemControl = Control.Clone(itemPrefab, true);
                fusedItemControl.SetParent(fusedItemContent);
				fusedItemControl.hasEffects = false;
                fusedItemControl.size = ItemControl.Size.Large;
                fusedMythic.gameObject.SetActive(true);
            }

            Game.instance.playerHud.shortMenu++;
            Game.instance.playerHud.showGemsMode++; 
            Game.instance.playerHud.showGoldMode++;

            Game.instance.gameScheduler.RegisterUpdate(UpdateFsm, Scheduler.Priority.Fsm);

            if (Game.instance.settingsManager.gameOptionToggles[SettingsManager.GameOption.Multicraft])
            {
                foreach (FusionItemControl fi in fusionItems)
                {
                    fi.onItemSelected += OnInventoryItemSelected;
                    fi.onItemUnselected += OnInventoryItemUnselected;
                }
            }
        }

        protected override void OnDestroy()
        {
            GiveBackItems();

            startButton.onClick.RemoveListener(StartFusion);
            exitButton.onClick.RemoveListener(OnCancelExit);
            clickCatcher.onClick.RemoveListener(OnCancelExit);
            clearButton.onClick.RemoveListener(GiveBackItems);
            shareButton.onClick.RemoveListener(OnShare);
            mythicButton.onToggled -= OnMythic;
            upgradeButton.onClick.RemoveListener(OnUpgrade);
            continueButton.onClick.RemoveListener(OnAcknowledgeResult);
            speedUpAnimClickCatcher.onClick.RemoveListener(OnSpeedUpAnims);

            Game.instance.playerHud.shortMenu--;
            Game.instance.playerHud.showGemsMode--;
            Game.instance.playerHud.showGoldMode--;

            Game.instance.gameScheduler.UnregisterUpdate(UpdateFsm);
            Game.instance.serverEventDispatcher.RemoveListener<CompleteFusionEvent>(OnComplete);

            foreach (FusionItemControl fi in fusionItems)
            {
                fi.onItemSelected -= OnInventoryItemSelected;
                fi.onItemUnselected -= OnInventoryItemUnselected;
            }

            ViewFusion view = Game.instance.views.GetView<ViewFusion>();
            if(view != null)
            {
                view.ViewClose();
            }

            _command.ClearCallbacks();
            base.OnDestroy();
        }

        private void OnInventoryItemSelected(InventoryItemProxy obj)
        {
            long fusionSlotsLeft = (long)Game.instance.user.Modifier.value["fusion_slots"];
            
            List<InventoryItemProxy> iips = new List<InventoryItemProxy>() { obj };
            foreach (FusionItemControl fi in fusionItems)
            {
                if (fi.iip != null)
                {
                    if(!fi.IsPremium)
                        iips.Add(fi.iip);
                    fusionSlotsLeft--;
                }
            }

            foreach (FusionItemControl fi in fusionItems)
            {
                if (fi.iip == null)
                {
                    fi.iip = obj;
                    string prefix = obj.Quality > 0 ? Game.instance.data.GetText("item_quality_" + obj.Quality) + " " : string.Empty;
                    string itemName = string.Format("{0}{1}", Utils.FormatTitle(prefix), Utils.FormatTitle(obj.ItemData.name));
                    Game.instance.infoPanel.PushMessage(string.Format(Game.instance.text.GetText("add_item_fusion"), itemName), InfoPanel.MessageType.Info);
                    fusionSlotsLeft--;
                    if (fusionSlotsLeft <= 0)
                    {
                        UnregisterAndCloseView();
                    }
                    break;
                }
            }
            UpdatePremiumItem(iips);
        }

        private void OnInventoryItemUnselected()
        {
            if (slot == null || !slot.Premium)
                return;
            List<InventoryItemProxy> iips = new List<InventoryItemProxy>();
            foreach (FusionItemControl fi in fusionItems)
            {
                if (fi.iip != null && !fi.IsPremium)
                {
                    iips.Add(fi.iip);
                }
            }
            UpdatePremiumItem(iips);
        }

        private void UpdatePremiumItem(List<InventoryItemProxy> iips)
        {
            if (slot != null && slot.Premium)
            {
                long pIndex = (long)Game.instance.user.Modifier.value["fusion_slots"] - 1;
                if (iips.Count > 0)
                {
                    iips.Sort((x, y) =>
                    {
                        if (x.Price == y.Price)
                            return y.Quality.CompareTo(x.Quality);
                        return y.Price.CompareTo(x.Price);
                    });
                    if (pIndex >= 0 && pIndex < fusionItems.Length)
                    {
                        Debug.Assert(fusionItems[pIndex].IsPremium);
                        fusionItems[pIndex].iip = iips[0];
                    }
                }
                else
                {
                    if (pIndex >= 0 && pIndex < fusionItems.Length)
                    {
                        fusionItems[pIndex].iip = null;

                    }
                }   
            }
        }

        private void UnregisterAndCloseView()
        {
            foreach (FusionItemControl fi in fusionItems)
            {
                fi.UnregisterFromView();
            }
            WindowManager.instance.RemoveCurrentFullScreenWindow();
        }

        private void InitIngredients(List<InventoryItemProxy> forcedIngredients = null)
        {
            bool isMythicFusion = false;
            ingredients.Clear();
            if(forcedIngredients != null)
            {
                foreach (InventoryItemProxy iip in forcedIngredients)
                {
                    if (iip.Quality >= InventoryItem.QUALITY6)
                        isMythicFusion = true;
                    ingredients.Add(iip);
                }
            }
            else
            {
                foreach (FusionItem fi in slot.FusionItems)
                {
                    ingredients.Add(Game.instance.user.InventoryProxy.SingleOrDefault(x => x.Uid == fi.Uid && x.Quality == fi.Quality));
                }
            }

            for (int i = 0; i < fusionItems.Length; i++)
            {
                FusionItemControl.FusionInfo fi = new FusionItemControl.FusionInfo()
                {
                    index = i,
                    mode = mode,
                    iip = forcedIngredients != null ? null : (ingredients.Count > i ? ingredients[i] : null)
                };
                fusionItems[i].data = fi;
                if(forcedIngredients != null)
                    fusionItems[i].iip = ingredients.Count > i ? ingredients[i] : null;
				fusionItems[i].itemControl.hasEffects = true;
                fi.onChange += OnFusionItemChange;
            }
            UpdatePremiumSlot();
            UpdatePremiumItem(ingredients);

            if(isMythicFusion)
            {
                mythicButton.isToggled = true;
                UpdateMythicMode();
            }
        }

        private void UpdateFsm()
        {
            if(machine.started)
                machine.Update();
        }

        private void OnFusionItemChange()
        {
            ingredients.Clear();
            for(int i = 0; i < fusionItems.Length; i++)
            {
                if (fusionItems[i].iip != null)
                    ingredients.Add(fusionItems[i].iip);
            }

            UpdateUI();
        }

		private void UpdateUI()
        {
			if(mode == Mode.Setup)
            {
                clearButton.gameObject.SetActive(true);
                mythicButton.gameObject.SetActive((long)Game.instance.user.Modifier.value["fusion_slots"] >= 5);
                mythicButton.text = Game.instance.text.GetText("mythic_fusion_title");
                startButton.interactable = ingredients.Count >= 2;
                upgradeButton.gameObject.SetActive(slot != null && !slot.Premium && (long)Game.instance.user.Modifier.value["fusion_slots"] >= 5);
                upgradeButton.text = string.Format(Game.instance.text.GetText("premium_slot_upgrade"), slot.Index + 1);
            }
            else
            {
                clearButton.gameObject.SetActive(false);
                mythicButton.gameObject.SetActive(false);
                upgradeButton.gameObject.SetActive(false);
                foreach (FusionItemControl fi in fusionItems)
                {
                    fi.mythicText.text = string.Empty;
                }
                foreach(FusionItemProbControl fip in mostProbableResults)
                {
                    fip.mythicText.text = string.Empty;
                }
                bestResult.mythicText.text = string.Empty;
            }
            UpdateMythicMode();
            UpdatePremiumSlot();
            UpdateResults();
        }
        
        private void UpdatePremiumSlot()
        {
            if (slot == null || !slot.Premium)
                return;

            long pIndex = (long)Game.instance.user.Modifier.value["fusion_slots"] - 1;
            foreach (FusionItemControl fi in fusionItems)
            {
                if (fi.IsPremium)
                {
                    fi.IsPremium = false;
                    fi.UpdateUI();
                }
            }
            if (pIndex >= 0 && pIndex < fusionItems.Length)
            {
                fusionItems[pIndex].IsPremium = true;
                fusionItems[pIndex].UpdateUI();
            }
        }

        private float GetFusionWeight(int quality)
		{
			switch(quality)
			{
				case 0:
				case 1:
					return Game.instance.data.Constants.fusionQuality1Weight;
				case 2:
					return Game.instance.data.Constants.fusionQuality2Weight;
				case 3:
					return Game.instance.data.Constants.fusionQuality3Weight;
				case 4:
					return Game.instance.data.Constants.fusionQuality4Weight;
				case 5:
					return Game.instance.data.Constants.fusionQuality5Weight;
                case 6:
                    return Game.instance.data.Constants.fusionQuality6Weight;
                case 7:
                    return Game.instance.data.Constants.fusionQuality7Weight;
                case 8:
                    return Game.instance.data.Constants.fusionQuality8Weight;
                case 9:
                    return Game.instance.data.Constants.fusionQuality9Weight;
                case 10:
                    return Game.instance.data.Constants.fusionQuality10Weight;
                case 11:
                    return Game.instance.data.Constants.fusionQuality11Weight;
                case 12:
                    return Game.instance.data.Constants.fusionQuality12Weight;
                case 13:
                    return Game.instance.data.Constants.fusionQuality13Weight;
                case 14:
                    return Game.instance.data.Constants.fusionQuality14Weight;
                case 15:
                    return Game.instance.data.Constants.fusionQuality15Weight;
                case 16:
                    return Game.instance.data.Constants.fusionQuality16Weight;
            }
			return 0;
		}

		private float GetFusionPower(int quality)
		{
			switch(quality)
			{
				case 0:
				case 1:
					return Game.instance.data.Constants.fusionQuality1Power;
				case 2:
					return Game.instance.data.Constants.fusionQuality2Power;
				case 3:
					return Game.instance.data.Constants.fusionQuality3Power;
				case 4:
					return Game.instance.data.Constants.fusionQuality4Power;
				case 5:
					return Game.instance.data.Constants.fusionQuality5Power;
                case 6:
                    return Game.instance.data.Constants.fusionQuality6Power;
                case 7:
                    return Game.instance.data.Constants.fusionQuality7Power;
                case 8:
                    return Game.instance.data.Constants.fusionQuality8Power;
                case 9:
                    return Game.instance.data.Constants.fusionQuality9Power;
                case 10:
                    return Game.instance.data.Constants.fusionQuality10Power;
                case 11:
                    return Game.instance.data.Constants.fusionQuality11Power;
                case 12:
                    return Game.instance.data.Constants.fusionQuality12Power;
                case 13:
                    return Game.instance.data.Constants.fusionQuality13Power;
                case 14:
                    return Game.instance.data.Constants.fusionQuality14Power;
                case 15:
                    return Game.instance.data.Constants.fusionQuality15Power;
                case 16:
                    return Game.instance.data.Constants.fusionQuality16Power;
            }
			return 1.0f;
		}

		private int GetNearestQualityLevel(int quality, Dictionary<int, int> qualities)
		{
			for(int i=quality; i>0; --i)
			{
				if(qualities.ContainsKey(i))
				{
					return qualities[i];
				}
			}
			return 1;
		}

		private float GetSuccessRate(float avgTargetQuality)
		{
			if(ingredients.Count >= 5 && avgTargetQuality < InventoryItem.QUALITY6)
			{
				return 1.0f;
			}
			else if(ingredients.Count >= 2)
			{
                if (avgTargetQuality <= 3)
                    avgTargetQuality -= 1;
				return Math.Min(1.0f, ingredients.Count * Game.instance.data.Constants.fusionSuccessBase / avgTargetQuality);
			}
            else
			{
				return 0;
			}
		}

		private void UpdateResults()
		{
			float craftTime = 0;
			if(ingredients.Count >= 2)
            {
				int minQ = 16;
				int maxQ = 0;
				float avgTargetQ = 0;
				List<InputInfo> inputValues = new List<InputInfo>();
				Dictionary<int, int> qualities = new Dictionary<int, int>();
				int highestQuality = 0;
				List<string> diffItems = new List<string>();
				//analyze input
				foreach(InventoryItemProxy iip in ingredients)
				{
					ItemData itemData = Game.instance.data.FindItem(iip.Uid);
					minQ = Mathf.Min(minQ, iip.Quality);
					maxQ = Mathf.Max(maxQ, iip.Quality);
					avgTargetQ += iip.Quality + 1;
					if(qualities.ContainsKey(iip.Quality))
						qualities[iip.Quality] = Mathf.Max(itemData.level, qualities[iip.Quality]);
					else
						qualities[iip.Quality] = itemData.level;

					InputInfo i = new InputInfo();
					i.itemData = itemData;
					i.quality = iip.Quality;
					inputValues.Add(i);

					if(diffItems.IndexOf(i.key) < 0)
					{
						diffItems.Add(i.key);
					}

					craftTime = Math.Max(craftTime, Game.instance.data.itemLevels[itemData.level-1].fusionTime);
					highestQuality = Mathf.Max(highestQuality, iip.Quality);
				}

				avgTargetQ /= ingredients.Count;

                float successPercent = GetSuccessRate(avgTargetQ) + Convert.ToSingle(Game.instance.user.Modifier.value["fusesuccess_multiplier"]);
                successRateText.text = String.Format("{0:0}%", Mathf.Clamp01(successPercent) * 100);

				craftTime *= Mathf.Pow(highestQuality + 1.0f, 1.25f);
				craftTime /= diffItems.Count;

                bool isLiveOpsFusion = Game.instance.user.LiveOps.bonuses.fusionTimeMultiplier.value != LiveOpsBonus.DEFAULT_VALUE;
                if (isLiveOpsFusion)
                    craftTime *= (float)Game.instance.user.LiveOps.bonuses.fusionTimeMultiplier.value;
                
                float fusionMultiplier = Convert.ToSingle(Game.instance.user.Modifier.value["fusetime_multiplier"]);
                craftTime *= 1 - fusionMultiplier;

                //create output points
                List<OutputInfo> outputValues = new List<OutputInfo>();
				foreach(InputInfo info in inputValues)
				{
					for(int q=minQ; q<=maxQ; ++q)
					{
						if(q >= info.quality)
						{
							OutputInfo o = new OutputInfo();
							o.itemData = info.itemData;
							o.quality = q + 1;
							outputValues.Add(o);
						}
					}
				}

				//assign weights to output values
				for(int x=0; x<outputValues.Count; ++x)
				{
					OutputInfo info = outputValues[x];
					info.weight = GetFusionWeight(info.quality);
					info.weight *= GetNearestQualityLevel(info.quality, qualities);
					info.weight /= Math.Pow(info.level, GetFusionPower(info.quality));
				}

				Dictionary<string, OutputInfo> mergedOutputValues = new Dictionary<string, OutputInfo>();
				double totalWeights = 0;
				for(int x=0; x<outputValues.Count; ++x)
				{
					OutputInfo info = outputValues[x];
					totalWeights += info.weight;
					if(mergedOutputValues.ContainsKey(info.key))
					{
						mergedOutputValues[info.key].weight += info.weight;
					}
					else
					{
						mergedOutputValues.Add(info.key, info);
					}
				}


				foreach(KeyValuePair<string, OutputInfo> kvp in mergedOutputValues)
				{
					kvp.Value.probability = kvp.Value.weight / totalWeights;
				}

				List<OutputInfo> results = mergedOutputValues.Values.ToList();
                List<OutputInfo> ioe = results.OrderByDescending(x => x.probability).Take(mostProbableResults.Length).ToList();
                for(int i = 0; i < mostProbableResults.Length; i++)
                {
                    if (ioe.Count > i)
                        mostProbableResults[i].data = ioe[i];
                    else
                        mostProbableResults[i].data = null;
                }
                results.Sort((x, y) =>
                {
                    if (x.level == y.level)
                        return y.quality.CompareTo(x.quality);
                    return y.level.CompareTo(x.level);
                });
                bestResult.data = results.FirstOrDefault();
            }
			else
			{
                successRateText.text = string.Empty;
                for(int i = 0; i < mostProbableResults.Length; i++)
                {
                    mostProbableResults[i].data = null;
                }
                bestResult.data = null;
			}

			if(craftTime > 0)
			{
				TimeSpan timeToCraft = TimeSpan.FromMilliseconds(craftTime * 1000);
				startButton.text = Utils.FormatTimeSpan(timeToCraft);
			}
			else
			{
                startButton.text = string.Empty;
			}
		}

		public void StartFusion()
		{
			if(slot.Type.value != null && slot.Done)
            {
				if(Game.instance.user.InventoryCount >= Game.instance.user.StorageCapacity)
				{
					Game.instance.infoPanel.PushMessage(Game.instance.text.GetText("warning_inventory_full"), InfoPanel.MessageType.Error);
                    OnExit();
				}
				else
				{
                    Game.instance.serverEventDispatcher.AddListener<CompleteFusionEvent>(OnComplete);
            		_command.Send("command", "CompleteFusion", "slot", slot.Index);
				}
            }
			else if(ingredients.Count >= 2)
            {
				List<Dictionary<string, object>> items = new List<Dictionary<string, object>>();

                foreach(FusionItemControl fi in fusionItems)
                {
                    fi.button.interactable = false;
                    if(fi.iip != null && !fi.IsPremium)
                    {
                        Dictionary<string, object> x = new Dictionary<string, object>();
                        x["u"] = fi.iip.Uid;
                        x["q"] = fi.iip.Quality;
                        items.Add(x);
                    }
                }
                _command.onServerResponseEvent += OnStartExit;
                _command.Send("command", "FuseItems", "slot", slot.Index, "items", items);
                //Predict the Start, give fake values
                slot.BeginCrafting(Slot.FUSION, null, 0, 1000, 1000, true);
            }
            startButton.interactable = false;
		}

        private void OnComplete(object sender, NetworkEventDispatcher.EventArgs e)
        {
            ItemData iData = null;
            int quality = 0;
            fusedMythic.text = string.Empty;
            if (!string.IsNullOrEmpty(slot.Uid))
            {
                iData = Game.instance.data.FindItem(slot.Uid);
                fusedItemControl.quality = Convert.ToInt32(slot.Quality.value);
                fusedItemControl.data = iData;
                fusionSuccess.text = Game.instance.text.GetText("fusion_successful");
                fusionSuccess.SetMaterial("success");
                fusionSuccessBorder.color = fusionSuccessColor;
                quality = fusedItemControl.quality;
                if (quality > InventoryItem.QUALITY6)
                    fusedMythic.text = string.Format("+{0}", quality - InventoryItem.QUALITY6);
            }
            else
            {
                fusionSuccess.text = Game.instance.text.GetText("fusion_failed");
                fusionSuccess.SetMaterial("failed");
                fusionSuccessBorder.color = fusionFailedColor;
            }

            continueButton.gameObject.SetActive(true);
            machine.TransitionTo(new FusionEndPhase2(this, iData != null, quality));
            Game.instance.serverEventDispatcher.RemoveListener<CompleteFusionEvent>(OnComplete);
        }

        private void OnStartExit()
        {
            Game.instance.user.Modifier.FireChangeEvent();
            _command.onServerResponseEvent -= OnStartExit;
            machine.TransitionTo(new FusionStartPhase1(this));
        }

        private void GiveBackItems()
        {
            if (mode == Mode.Setup)
            {
                //Give back all items
                foreach (FusionItemControl fi in fusionItems)
                {
                    fi.iip = null;
                }
            }
        }

        private void OnMythic()
        {
            GiveBackItems();
            UpdateMythicMode();
        }

        private void UpdateMythicMode()
        {
            foreach (FusionItemControl fi in fusionItems)
            {
                fi.FusionQualityMaximum = !mythicButton.isToggled ? InventoryItem.QUALITY5 : 0;
                fi.FusionQualityMinumum = !mythicButton.isToggled ? 0 : InventoryItem.QUALITY6;
            }
        }

        private void OnShare()
        {
            ScreenshotHelper.instance.OnShareScreenshotDone += OnShareScreenshotDone;
            ScreenshotHelper.instance.Share();
        }

        private void OnShareScreenshotDone(bool done)
        {
            ScreenshotHelper.instance.OnShareScreenshotDone -= OnShareScreenshotDone;
        }

        private void OnUpgrade()
        {
            string gameobjectName = string.Format("PremiumSlot_{0}", User.UNLOCK_SLOT_FUSION);
            GameObject go = Game.instance.assets.CloneGameObject(gameobjectName);
            if (go != null)
            {
                PremiumSlotsPopupControl popup = go.GetComponent<PremiumSlotsPopupControl>();
                if (popup != null)
                {
                    popup.isFusion = true;
                    popup.data = slot;
                    popup.OnBuyNow += () =>
                    {
                        GiveBackItems();
                        UpdateUI();
                    };
                    WindowManager.instance.SetPopupWindow(popup, true);
                }
            }
        }

        private void OnSpeedUpAnims()
        {
            speedUpAnim = true;
        }

        private void OnCancelExit()
        {
            if (!CanGoBack())
                return;

            OnExit();
        }

        private void OnExit()
        {
            WindowManager.instance.RemoveCurrentFullScreenWindow();
        }

        private void OnAcknowledgeResult()
        {
            continueButton.gameObject.SetActive(false);
            machine.TransitionTo(new FusionEndPhase3(this));
        }

        private void StopEndSound()
        {
            if(endSoundResult !=  null && endSoundResult.ActingVariation != null && endSoundResult.ActingVariation.IsPlaying)
            {
                endSoundResult.ActingVariation.Stop();
            }
        }

#region Animation
        #region Animation Start
        private class FusionStartPhase1 : State
        {
            private FusionControl _fc;
            public FusionStartPhase1(FusionControl fc)
            {
                _fc = fc;
            }

            protected override void TransitionIn(State previous)
            {
                base.TransitionIn(previous);

                _fc.speedUpAnim = false;
                _fc.speedUpAnimClickCatcher.gameObject.SetActive(true);
                _fc.anim.Play(_fc.animStartPhase1.name, PlayMode.StopAll);
                foreach(FusionItemControl fi in _fc.fusionItems)
                {
                    fi.button.button.enabled = false;
                    fi.animator.SetTrigger("Invisible");
                }
            }
            
            protected override State.Result Update()
            {
                if (_fc.anim.isPlaying)
                    return Result.Continue;
                return Result.TransitionTo(new FusionItemUp(_fc, 0));
            }
        }

        private class FusionItemUp : State
        {
            private FusionControl _fc;
            private int _index;

            private bool _isLast;
            private bool _skip;

            public FusionItemUp(FusionControl fc, int index)
            {
                _fc = fc;
                _index = index;
            }

            protected override void TransitionIn(State previous)
            {
                base.TransitionIn(previous);
                _skip = _fc.fusionItems[_index].iip == null;
                _isLast = _index + 1 >= _fc.fusionItems.Length;

                if (!_skip)
                    _fc.fusionItems[_index].anim.Play(_fc.fusionItems[_index].animFloatUp.name, PlayMode.StopAll);
            }

            protected override State.Result Update()
            {
                if(_skip && !_isLast)
                    return Result.TransitionTo(new FusionItemUp(_fc, _index + 1));
                if(!_isLast)
                    return Result.TransitionTo(new TimerUtils.CallbackInState(_fc.speedUpAnim ? 0 : 0.2f, new FusionItemUp(_fc, _index + 1)));
                if (_fc.fusionItems[_index].anim.isPlaying)
                    return Result.Continue;
                return Result.TransitionTo(new FusionItemIn(_fc, 0));
            }
        }

        private class FusionItemIn : State
        {
            private FusionControl _fc;
            private int _index;

            private bool _skip;
            private bool _isLast;

            public FusionItemIn(FusionControl fc, int index)
            {
                _fc = fc;
                _index = index;
            }

            protected override void TransitionIn(State previous)
            {
                base.TransitionIn(previous);
                _skip = _fc.fusionItems[_index].iip == null;
                _isLast = _index + 1 >= _fc.fusionItems.Length;

                if (!_skip)
                {
                    _fc.StartCoroutine(_fc.fusionItems[_index].anim.PlayWithOptions(PlayMode.StopAll, _fc.fusionItems[_index].animFloatIn.name, null, () =>
                    {
                        Game.instance.fxManager.SetParentAndPos(_fc.splashTraget.gameObject, "FusionItemInFx");
                    }));
                    Game.instance.soundManager.PlaySound(eSoundId.eSFX_FusionStart);
                }
            }

            protected override State.Result Update()
            {
                if(_skip && !_isLast)
                    return Result.TransitionTo(new FusionItemIn(_fc, _index + 1));
                if(!_isLast)
                    return Result.TransitionTo(new TimerUtils.CallbackInState(_fc.speedUpAnim ? 0 : 0.2f, new FusionItemIn(_fc, _index + 1)));
                if (_fc.fusionItems[_index].anim.isPlaying)
                    return Result.Continue;
                return Result.TransitionTo(new FusionStartPhase2(_fc));
            }
        }

        private class FusionStartPhase2 : State
        {
            private FusionControl _fc;

            public FusionStartPhase2(FusionControl fc)
            {
                _fc = fc;
            }

            protected override void TransitionIn(State previous)
            {
                base.TransitionIn(previous);

                _fc.anim.Play(_fc.animStartPhase2.name, PlayMode.StopAll);
            }

            protected override State.Result Update()
            {
                if (_fc.anim.isPlaying)
                    return Result.Continue;
                return Result.Stop;
            }

            protected override void TransitionOut(State next)
            {
                base.TransitionOut(next);
                _fc.OnExit();
            }
        }
        #endregion

        #region Animation End
        private class FusionEndPhase1 : State
        {
            private FusionControl _fc;

            public FusionEndPhase1(FusionControl fc)
            {
                _fc = fc;
            }

            protected override void TransitionIn(State previous)
            {
                base.TransitionIn(previous);

                foreach (FusionItemControl fi in _fc.fusionItems)
                {
                    fi.animator.CrossFade("Invisible", 0, 0, 1f);
                    fi.anim[fi.animFloatIn.name].normalizedTime = 1;
                    fi.anim.Play(fi.animFloatIn.name, PlayMode.StopAll);
					fi.itemControl.hasEffects = false;
                }

                _fc.anim.Play(_fc.animEndPhase1.name, PlayMode.StopAll);
                _fc.speedUpAnimClickCatcher.gameObject.SetActive(true);
            }

            protected override State.Result Update()
            {
                if (_fc.anim.isPlaying)
                    return Result.Continue;
                return Result.Stop;
            }

            protected override void TransitionOut(State next)
            {
                base.TransitionOut(next);
                _fc.StartFusion();
            }
        }

        private class FusionEndPhase2 : State
        {
            private FusionControl _fc;
            private bool _isSuccess;
            private int _quality;

            public FusionEndPhase2(FusionControl fc, bool isSuccess, int quality)
            {
                _fc = fc;
                _isSuccess = isSuccess;
                _quality = quality;
            }

            protected override void TransitionIn(State previous)
            {
                base.TransitionIn(previous);

                _fc.anim.Play(_fc.animEndPhase2.name, PlayMode.StopAll);
                _fc.endSoundResult = Game.instance.soundManager.PlaySound(eSoundId.eSFX_FusionEnd);
            }

            protected override State.Result Update()
            {
                if (_fc.anim.isPlaying)
                    return Result.Continue;
                return State.Result.TransitionTo(new FusionEndPhase2point2(_fc, _isSuccess, _quality));
            }
        }

        private class FusionEndPhase2point2 : State
        {
            private FusionControl _fc;
            private bool _isSuccess;
            private int _quality;

            public FusionEndPhase2point2(FusionControl fc, bool isSuccess, int quality)
            {
                _fc = fc;
                _isSuccess = isSuccess;
                _quality = quality;
            }

            protected override void TransitionIn(State previous)
            {
                base.TransitionIn(previous);

                _fc.StopEndSound();
                if (_isSuccess)
                {
                    _fc.anim.Play(_fc.animEndPhase2Success.name, PlayMode.StopAll);
                    Game.instance.soundManager.PlaySound(eSoundId.eSFX_FusionSuccessful);
                    _fc.shareButton.gameObject.SetActive(_quality >= InventoryItem.QUALITY5);
                }
                else
                {
                    _fc.successGlowPanel.gameObject.SetActive(false);
                    _fc.anim.Play(_fc.animEndPhase2Failure.name, PlayMode.StopAll);
                    Game.instance.soundManager.PlaySound(eSoundId.eSFX_FusionFailed);
                }
            }

            protected override State.Result Update()
            {
                if (_fc.anim.isPlaying)
                    return Result.Continue;

                _fc.speedUpAnim = false;
                _fc.speedUpAnimClickCatcher.gameObject.SetActive(false);
                return Result.Stop;
            }

            protected override void TransitionOut(State next)
            {
                if (_fc.fusedItemControl != null)
                {
                    _fc.fusedItemControl.hasEffects = true;
                    _fc.fusedMythic.gameObject.SetActive(true);
                }
					

                base.TransitionOut(next);
            }
        }

        private class FusionEndPhase3 : State
        {
            private FusionControl _fc;

            public FusionEndPhase3(FusionControl fc)
            {
                _fc = fc;
            }

            protected override void TransitionIn(State previous)
            {
                base.TransitionIn(previous);

                bool hasFailed = string.IsNullOrEmpty(_fc.slot.Uid.value);
                List<InventoryItemProxy> iips = new List<InventoryItemProxy>();
                if (hasFailed)
                {
                    foreach (FusionItem fi in _fc.slot.FusionItems)
                    {
                        InventoryItemProxy iip = Game.instance.user.InventoryProxy.SingleOrDefault(x => x.Uid == fi.Uid && x.Quality == fi.Quality);
                        iips.Add(iip);
                    }   
                }
                _fc.slot.EndCrafting();
                _fc.InitIngredients(hasFailed ? iips : null);
                _fc.UpdateUI();

                _fc.startButton.gameObject.SetActive(true);
                _fc.anim.Play(_fc.animEndPhase3.name, PlayMode.StopAll);
				_fc.fusedItemControl.hasEffects = false;
                _fc.fusedMythic.gameObject.SetActive(false);
                foreach (FusionItemControl fi in _fc.fusionItems)
                {
                    fi.animator.SetTrigger("Reset");

                    fi.anim.Play(fi.animFloatOut.name, PlayMode.StopAll);
                }
            }

            protected override State.Result Update()
            {
                if (_fc.anim.isPlaying)
                    return Result.Continue;
                foreach (FusionItemControl fi in _fc.fusionItems)
                {
                    if (fi.anim.isPlaying)
                        return Result.Continue;
                }
                return Result.Stop;
            }

            protected override void TransitionOut(State next)
            {
                base.TransitionOut(next);

                foreach (FusionItemControl fi in _fc.fusionItems)
                {
                    fi.button.button.enabled = true;
                    fi.anim[fi.animFloatOut.name].normalizedTime = 1;
                    fi.anim.Play(fi.animFloatOut.name, PlayMode.StopAll);
                }
                _fc.shareButton.gameObject.SetActive(false);
                Game.instance.user.Modifier.FireChangeEvent();
            }
        }
        #endregion
#endregion
    }
}

﻿using System.Linq;
using UAlbion.Formats.AssetIds;
using UAlbion.Game.Entities;
using UAlbion.Game.Events;
using UAlbion.Game.State;

namespace UAlbion.Game.Gui.Inventory
{
    public class InventoryWeightLabel : UiElement
    {
        readonly PartyCharacterId _activeCharacter;
        int _version = 0;

        static readonly HandlerSet Handlers = new HandlerSet(
            H<InventoryWeightLabel, InventoryChangedEvent>((x,e) => x._version++),
            H<InventoryWeightLabel, SetLanguageEvent>((x, e) => x._version++),
            H<InventoryWeightLabel, UiHoverEvent>((x, e) =>
            {
                x.Hover(); 
                e.Propagating = false;
            }),
            H<InventoryWeightLabel, UiBlurEvent>((x, _) => x.Raise(new HoverTextEvent(""))));

        public InventoryWeightLabel(PartyCharacterId activeCharacter) : base(Handlers)
        {
            _activeCharacter = activeCharacter;
            var source = new DynamicText(() =>
            {
                var state = Resolve<IStateManager>();
                var assets = Resolve<IAssetManager>();
                var settings = Resolve<ISettings>();

                var player = state.State.GetPartyMember(_activeCharacter);
                var weight = 12; // TODO: Include items!
                var template = assets.LoadString(SystemTextId.Inv_WeightNKg, settings.Language); // Weight : %d Kg
                return new 
                    TextFormatter(assets, settings.Language)
                    .NoWrap()
                    .Centre()
                    .Format(template, weight)
                    .Item1;
            }, () => _version);

            Children.Add(new ButtonFrame(new Text(source))
            {
                State = ButtonState.Pressed,
                Padding = 0
            });
        }

        void Hover()
        {
            var state = Resolve<IStateManager>();
            var assets = Resolve<IAssetManager>();
            var settings = Resolve<ISettings>();

            var player = state.State.GetPartyMember(_activeCharacter);
            var weight = 12341; // TODO: Include items!
            var maxWeight = 38000; // TODO: Implement

            // Carried Weight : %ld of %ld g
            var template = assets.LoadString(SystemTextId.Inv_CarriedWeightNdOfNdG, settings.Language);
            var (text, _) = new TextFormatter(assets, settings.Language).Format(template, weight, maxWeight);
            Raise(new HoverTextEvent(text.First().Text));
        }
    }
}
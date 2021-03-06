﻿using System.IO;
using UAlbion.Api;

namespace UAlbion.Formats.MapEvents
{
    public class QueryEvent : IEvent
    {
        public enum QueryType : byte
        {
            TemporarySwitch = 0x0,
            HasPartyMember = 0x5,
            InventoryHasItem = 0x6,
            UsedItemId = 0x7,
            PreviousActionResult = 0x9,
            IsScriptDebugModeActive = 0xA,
            IsNpcActive = 0xE,
            HasEnoughGold = 0xF,
            RandomChance = 0x11,
            ChosenVerb = 0x14,
            IsPartyMemberConscious = 0x15,
            IsPartyMemberLeader = 0x1A,
            Ticker = 0x1C,
            CurrentMapId = 0x1D,
            PromptPlayer = 0x1F,
            TriggerType = 0x20,
            EventAlreadyUsed = 0x22,
            IsDemoVersion = 0x23,
            PromptPlayerNumeric = 0x2B
        }

        public enum QueryOperation
        {
            Unk0,
            Unk1,
            Unk2,
            Equals,
        }

        public static BranchNode Load(BinaryReader br, int id, MapEventType type)
        {
            var subType = (QueryType)br.ReadByte(); // 1
            switch (subType)
            {
                case QueryType.InventoryHasItem:
                case QueryType.UsedItemId:
                    return QueryItemEvent.Load(br, id, subType);

                case QueryType.ChosenVerb:
                    return QueryVerbEvent.Load(br, id);

                case QueryType.PreviousActionResult:
                case QueryType.Ticker:
                case QueryType.CurrentMapId:
                case QueryType.PromptPlayer:
                case QueryType.TriggerType:
                default:
                    break;
            }

            var e = new QueryEvent
            {
                SubType = subType,
                Unk2 = br.ReadByte(), // 2
                Unk3 = br.ReadByte(), // 3
                Unk4 = br.ReadByte(), // 4
                Unk5 = br.ReadByte(), // 5
                Argument = br.ReadUInt16(), // 6
            };

            ushort? falseEventId = br.ReadUInt16(); // 8
            if (falseEventId == 0xffff)
                falseEventId = null;
            return new BranchNode(id, e, falseEventId);
        }

        public byte Unk2 { get; protected set; } // method to use for check?
        public byte Unk3 { get; protected set; }
        public byte Unk4 { get; protected set; }
        public byte Unk5 { get; protected set; }

        public QueryType SubType { get; protected set; }
        public ushort Argument { get; protected set; }

        public override string ToString() => $"query({SubType} {Argument} ({Unk2} {Unk3} {Unk4} {Unk5}))";
    }
}

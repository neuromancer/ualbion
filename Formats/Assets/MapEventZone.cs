﻿using System;
using System.Diagnostics;
using System.IO;
using UAlbion.Formats.MapEvents;

namespace UAlbion.Formats.Assets
{
    public class MapEventZone
    {
        [Flags]
        public enum TriggerType : ushort
        {
            Normal = 1 << 0,
            Examine = 1 << 1,
            Touch = 1 << 2,
            Speak = 1 << 3,
            UseItem = 1 << 4,
            MapInit = 1 << 5,
            EveryStep = 1 << 6,
            EveryHour = 1 << 7,
            EveryDay = 1 << 8,
            Default = 1 << 9,
            Action = 1 << 10,
            Npc = 1 << 11,
            Take = 1 << 12,
            Unk13 = 1 << 13,
            Unk14 = 1 << 14,
            Unk15 = 1 << 15,
        }

        public bool Global;
        public byte Unk1 { get; set; }
        public ushort X;
        public ushort Y;
        public TriggerType Trigger;
        public ushort EventNumber;
        public IEventNode Event { get; set; }

        public static MapEventZone LoadGlobalZone(BinaryReader br)
        {
            var zone = new MapEventZone();
            zone.Global = true;
            zone.X = br.ReadUInt16(); // +0
            Debug.Assert(zone.X == 0);
            zone.Trigger = (TriggerType) br.ReadUInt16(); // +2
            zone.EventNumber = br.ReadUInt16(); // +4
            return zone;
        }

        public static MapEventZone LoadZone(BinaryReader br, ushort y)
        {
            var zone = new MapEventZone();
            zone.X = (ushort)(br.ReadByte() - 1); // +0
            Debug.Assert(zone.X != 0xffff);
            zone.Unk1 = br.ReadByte(); // +1
            zone.Y = y;
            zone.Trigger = (TriggerType)br.ReadUInt16(); // +2
            zone.EventNumber = br.ReadUInt16(); // +4
            return zone;
        }

        public override string ToString() => $"Zone ({X}, {Y}) T:{Trigger} Mode:{Unk1} E:{EventNumber}";
    }
}
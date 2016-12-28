﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SilkroadSecurityApi;
namespace SilkroadInformationAPI.Client.Packets.Inventory
{
    class ParseItem
    {
        public static Information.Item Parse(Packet p)
        {
            int slot = p.ReadInt8();
            p.ReadInt32(); //Empty DWORD

            int itemID = p.ReadInt32();
            Information.Item item = new Information.Item(itemID);
            item.Slot = slot;

            Console.WriteLine(item.MediaName);
            Console.WriteLine(item.Type);

            if (item.Classes.C == 3 && item.Classes.D == 1)
            { //Armor || Jewlery || Weapon || Shield || Job suites || Devils || Flags
                if (item.Classes.E == 6)
                    item.Type = ItemType.Weapon;
                else if (item.Classes.E == 4)
                    item.Type = ItemType.Shield;
                else if (item.Classes.E == 12 || item.Classes.E == 5)
                    item.Type = ItemType.Accessory;
                else
                    item.Type = ItemType.Protector;

                item.PlusValue = p.ReadInt8();   //Plus value

                ulong variance = p.ReadUInt64();
                item.Stats = Utility.CalculateWhiteStats(variance, item.Type);
                item.Stats.Add("DURABILITY", p.ReadInt32());

                int countB = p.ReadInt8(); //Blue count
                item.Blues = Alchemy.ParseInfo.ParseBlues(p, countB);

                p.ReadInt8(); // Can add sockets
                int socks = p.ReadInt8(); // Sockets
                for (int j = 0; j < socks; j++)
                {
                    p.ReadInt8(); //Sock number
                    p.ReadInt32(); //Sock ID
                    p.ReadInt32(); //Sock value
                }
                p.ReadInt8(); // Can add adv
                int advCheck = p.ReadInt8(); // Adv count only 1 though, can add more through db only
                if (advCheck == 0x01)
                {
                    item.HasAdvance = true;
                    p.ReadInt8(); //00
                    p.ReadInt32(); //ADV ID
                    p.ReadInt32(); //ADV plus value
                }
            }
            else if (item.Type == ItemType.PickupPet || item.Type == ItemType.AttackPet)
            {
                int flagCheck = p.ReadInt8(); //State 1=Not opened yet, 2=Summoned, 3=Not summoned, 04=Expired/Dead
                if (flagCheck != 1)
                {
                    p.ReadInt32(); //UNK
                    p.ReadAscii(); //PET Name
                    if (item.Type == ItemType.PickupPet)
                    {
                        p.ReadInt32(); //Time date?
                    }
                    p.ReadInt8(); //Unk
                }
            }
            else if (item.Type == ItemType.ItemExchangeCoupon)
            {
                p.ReadInt16(); //count
                int countB = p.ReadInt8(); //Blue count
                for (int k = 0; k < countB; k++)
                {
                    p.ReadInt32(); //Magic option ID
                    p.ReadInt32(); //Value
                }
            }
            else if (item.Type == ItemType.Stones)
            {
                item.Count = p.ReadInt16(); //count
                p.ReadInt8(); //AttributeAssimilationProbability
            }
            else if (item.Type == ItemType.MagicCube || item.Type == ItemType.MonsterMask)
            { //Item exchange coupon, Elixirs cube
                p.ReadInt32(); //Model ID
            }
            else
            {
                item.Count = p.ReadInt16();
            }

            if (item.MediaName.Contains("RECIPE_WEAPON"))
                item.Type = ItemType.WeaponElixir;
            else if (item.MediaName.Contains("RECIPE_SHIELD"))
                item.Type = ItemType.ShieldElixir;
            else if (item.MediaName.Contains("RECIPE_ARMOR"))
                item.Type = ItemType.ProtectorElixir;
            else if (item.MediaName.Contains("RECIPE_ACCESSARY"))
                item.Type = ItemType.AccessoryElixir;
            else if (item.MediaName.Contains("PROB_UP"))
                item.Type = ItemType.LuckyPowder;
            else if (item.MediaName.Contains("MAGICSTONE_ATHANASIA"))
                item.Type = ItemType.ImmortalStone;
            else if (item.MediaName.Contains("MAGICSTONE_LUCK"))
                item.Type = ItemType.LuckStone;
            else if (item.MediaName.Contains("MAGICSTONE_SOLID"))
                item.Type = ItemType.SteadyStone;
            else if (item.MediaName.Contains("MAGICSTONE_ASTRAL"))
                item.Type = ItemType.AstralStone;

            //Console.WriteLine(Data.MediaItems[item.ID].TranslationName + " : " + item.Count + " : " + item.Type);

            return item;
        }
    }
}

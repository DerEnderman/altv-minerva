﻿using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Minerva.Server.Core.Contracts.Enums;
using Minerva.Server.Core.Contracts.Models.Base;

namespace Minerva.Server.Core.Contracts.Models.Database
{
    public class Character
        : PositionRotationEntityBase
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public ulong AccountId { get; set; }
        public Account Account { get; set; }

        public string Model { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public DateTime Birthday { get; set; }

        public ushort Health { get; set; }

        public ushort Armor { get; set; }

        public uint Cash { get; set; }

        public uint Hunger { get; set; }

        public uint Thirst { get; set; }

        public string AppearanceParents { get; set; }

        public string AppearanceFaceFeatures { get; set; }

        public string AppearanceDetails { get; set; }

        public string AppearanceHair { get; set; }

        public string AppearanceClothes { get; set; }

        [NotMapped]
        public string Name => $"{FirstName} {LastName}";

        [NotMapped]
        public Gender Gender
        {
            get
            {
                switch (Model)
                {
                    case "mp_m_freemode_01":
                        return Gender.Male;

                    case "mp_f_freemode_01":
                        return Gender.Female;

                    default:
                        return Gender.None;
                }
            }
        }
    }
}

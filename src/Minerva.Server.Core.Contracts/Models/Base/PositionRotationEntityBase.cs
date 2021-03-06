﻿using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;

namespace Minerva.Server.Core.Contracts.Models.Base
{
    public abstract class PositionRotationEntityBase
        : PositionEntityBase
    {
        public float RotationX { get; set; }

        public float RotationY { get; set; }

        public float RotationZ { get; set; }

        [NotMapped]
        public Vector3 Rotation
        {
            get => new Vector3(RotationX, RotationY, RotationZ);
            set
            {
                RotationX = value.X;
                RotationY = value.Y;
                RotationZ = value.Z;
            }
        }
    }
}

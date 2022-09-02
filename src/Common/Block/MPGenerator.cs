using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common.Entities;
using VSSurvivalMod;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using System.Collections.Generic;
using System.Text;
using SpiceSharp;
using SpiceSharp.Components;
using SpiceSharp.Simulations;

namespace ElectricalRevolution
{
	public class MPGenerator : BlockMPBase
	{
		public bool IsOrientedTo(BlockFacing facing)
		{
			string dirs = base.LastCodePart(0);
			return dirs[0] == facing.Code[0] || (dirs.Length > 1 && dirs[1] == facing.Code[0]);
		}
		public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
		{
			return this.IsOrientedTo(face);
		}

		public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
		{
			if (!this.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
			{
				return false;
			}
			foreach (BlockFacing face in BlockFacing.HORIZONTALS)
			{
				BlockPos pos = blockSel.Position.AddCopy(face);
				IMechanicalPowerBlock block = world.BlockAccessor.GetBlock(pos) as IMechanicalPowerBlock;
				if (block != null && block.HasMechPowerConnectorAt(world, pos, face.Opposite))
				{
					AssetLocation loc = new AssetLocation(base.Code.Domain,base.FirstCodePart(0) + "-" + face.Opposite.Code[0].ToString() + face.Code[0].ToString());
					Block toPlaceBlock = world.GetBlock(loc);
					if (toPlaceBlock == null)
					{
						loc = new AssetLocation(base.Code.Domain, base.FirstCodePart(0) + "-" + face.Code[0].ToString() + face.Opposite.Code[0].ToString());
						toPlaceBlock = world.GetBlock(loc);
					}
					if (toPlaceBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack))
					{
						block.DidConnectAt(world, pos, face.Opposite);
						this.WasPlaced(world, blockSel.Position, face);
						return true;
					}
				}
			}
			if (base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode))
			{
				this.WasPlaced(world, blockSel.Position, null);
				return true;
			}
			return false;
		}

		public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
		{
			BlockEntity blockEntity = world.BlockAccessor.GetBlockEntity(pos);
			BEBehaviorMPGenerator bempgenerator = (blockEntity != null) ? blockEntity.GetBehavior<BEBehaviorMPGenerator>() : null;
			if (bempgenerator != null && !bempgenerator.IsAttachedToBlock())
			{
				foreach (BlockFacing face in BlockFacing.HORIZONTALS)
				{
					BlockPos npos = pos.AddCopy(face);
					BlockAngledGears blockagears = world.BlockAccessor.GetBlock(npos) as BlockAngledGears;
					if (blockagears != null && blockagears.Facings.Contains(face.Opposite) && blockagears.Facings.Length == 1)
					{
						world.BlockAccessor.BreakBlock(npos, null, 1f);
					}
				}
			}
			base.OnNeighbourBlockChange(world, pos, neibpos);
		}
		public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
		{
		}

	}
}
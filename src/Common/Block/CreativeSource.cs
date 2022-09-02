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
	public class CreativeSource : Block
	{
		public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
		{
			BlockEntity blockEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position);
			BEBehaviorCreativeConverter be = blockEntity?.GetBehavior<BEBehaviorCreativeConverter>();
			if (be != null)
			{
				return be.OnInteract(byPlayer);
			}
			return base.OnBlockInteractStart(world, byPlayer, blockSel);
		}
	}
}
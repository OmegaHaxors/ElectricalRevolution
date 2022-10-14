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
	public class LightBlockEntity : BlockEntity
	{
		private BEBehaviorElectricalNode nodebehavior = null;
		//private bool powered = false;
		private byte[] lightHsv = new byte[]{7,4,18};
		//Random rred = new Random(765364143);
		//Random rblue = new Random(742543);
		//Random rgreen = new Random(321467);

		public override void Initialize(ICoreAPI api)
		{
			base.Initialize(api);
			//RegisterGameTickListener(UpdateNBT, 1000);//only runs on the server (set to 100)
		}

		public override void CreateBehaviors(Block block, IWorldAccessor worldForResolve)
		{
			base.CreateBehaviors(block, worldForResolve);
			this.nodebehavior = base.GetBehavior<BEBehaviorElectricalNode>();
		}

        public byte[] GetLightHsv()
        {
            return lightHsv;
        }

		public void SetLightLevel(byte lightvalue)
        {
            byte oldvalue = lightHsv[2]; lightHsv[2] = lightvalue;SendLightUpdate();
        }

		public void SetLightColor(int red, int green, int blue)
		{
			int[] HSVfromRGB = ColorUtil.RgbToHsvInts(red,green,blue);
			lightHsv[0] = (byte)HSVfromRGB[0];
			lightHsv[1] = (byte)HSVfromRGB[1];
			lightHsv[2] = (byte)HSVfromRGB[2];
			SendLightUpdate();
		}

		public void SetLightHsv(byte[] hsv)
        {
            lightHsv = hsv; SendLightUpdate();
        }

        ///do this when you change the light level
		public void SendLightUpdate()
		{
			Api.World.BlockAccessor.MarkBlockModified(base.Pos);
		}

        ///set the light level from the supplied voltage. If no arg, it will take from Tree
		public void SetLightLevelFromVoltage(double voltage = -1f)
		{
			//byte red = (byte)rred.Next(0,22);
			//byte green = (byte)rblue.Next(0,22);
			//byte blue = (byte)rgreen.Next(0,22);
			if(voltage < 0 && this.GetBehavior<BEBehaviorElectricalConverter>() != null)
			{
    			BlockEntity block = Api.World.BlockAccessor.GetBlockEntity(base.Pos);
    			voltage = this.GetBehavior<BEBehaviorElectricalConverter>().Voltage;
			}
			SetLightLevel((byte)(voltage * 2.2));
			if(Api.Side == EnumAppSide.Server)
            {
                Api.World.PlaySoundAt(new AssetLocation("autosifter:sounds/sifterworking"),Pos.X,Pos.Y,Pos.Z);
            }
		}
	}
}
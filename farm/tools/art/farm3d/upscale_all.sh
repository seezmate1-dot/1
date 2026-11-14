#!/bin/bash
B=/home/user/1/farm/unity/SunnyValley/Assets/SunnyValley/Resources/Art/Ch1/Backgrounds
mkdir -p ~/farm3d/bg/up
for z in Orchard Garden Yard House Barn Coop Mill Meadow Pond; do
  [ -f ~/farm3d/bg/up/$z.png ] || ~/sdenv/bin/python ~/farm3d/upscale.py $B/$z.jpg ~/models/esrgan/anime6B.pth ~/farm3d/bg/up/$z.png 2
done

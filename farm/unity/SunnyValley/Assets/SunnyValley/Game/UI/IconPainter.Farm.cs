namespace SunnyValley.Game.UI
{
    /// <summary>
    /// Placeholder icons of the farm economy (chapter 2) until the art lands: the side buttons (fields, animals,
    /// workshop, barn, town), the truck, XP, residents, lots and beauty, every good of the barn («item.wheat»), the
    /// tools («tool.hammer») and the animals («animal.cow»). The same simple rimmed shapes as the rest; goods of one
    /// kind share a shape in their own colour (a sack of feed, a jar of jam, a pie).
    /// </summary>
    public static partial class IconPainter
    {
        const uint Cream = 0xFFF3D6FF, CreamDark = 0xC9A86AFF, Pink = 0xFF9EB5FF, PinkDark = 0xC85A78FF, Black = 0x2B2B33FF;

        static bool Economy(Canvas c, string name)
        {
            if (name.StartsWith("item."))
                return Item(c, name.Substring(5));
            if (name.StartsWith("tool."))
                return Tool(c, name.Substring(5));
            if (name.StartsWith("animal."))
                return Animal(c, name.Substring(7));
            switch (name)
            {
                case "fields":
                    c.Rimmed(Box(.5f, .3f, .4f, .16f, .08f), Brown, BrownDark, .03f);
                    for (int i = 0; i < 3; i++)
                    {
                        float x = .26f + i * .24f;
                        c.Fill(Line(x, .4f, x, .62f, .025f), GreenDark);
                        c.Rimmed(Ellipse(x - .06f, .64f, .07f, .04f), Green, GreenDark, .015f);
                        c.Rimmed(Ellipse(x + .06f, .7f, .07f, .04f), Green, GreenDark, .015f);
                    }
                    return true;
                case "animals":
                    return Animal(c, "chicken");
                case "workshop":
                    c.Rimmed(Box(.42f, .34f, .3f, .24f, .03f), 0xE9CF9AFF, BrownDark, .03f);
                    c.Rimmed(Poly(.08f, .56f, .76f, .56f, .42f, .84f), Red, RedDark, .03f);
                    c.Fill(Box(.42f, .24f, .08f, .14f, .02f), BrownDark);
                    c.Rimmed(Gear(.76f, .3f), Gray, GrayDark, .02f);
                    return true;
                case "barn":
                    c.Rimmed(Box(.5f, .34f, .36f, .26f, .02f), Red, RedDark, .03f);
                    c.Rimmed(Poly(.08f, .58f, .92f, .58f, .74f, .8f, .5f, .9f, .26f, .8f), 0xB8402CFF, RedDark, .03f);
                    c.Fill(Box(.5f, .26f, .13f, .18f, .02f), White);
                    c.Fill(Line(.38f, .1f, .62f, .42f, .02f), RedDark);
                    c.Fill(Line(.38f, .42f, .62f, .1f, .02f), RedDark);
                    return true;
                case "town":
                    c.Rimmed(Box(.5f, .32f, .3f, .24f, .03f), Cream, CreamDark, .03f);
                    c.Rimmed(Poly(.12f, .54f, .88f, .54f, .5f, .88f), Blue, BlueDark, .03f);
                    c.Fill(Box(.38f, .24f, .07f, .14f, .02f), Brown);
                    c.Rimmed(Box(.64f, .36f, .08f, .07f, .01f), 0x9ED9F6FF, BlueDark, .015f);
                    return true;
                case "truck":
                    c.Rimmed(Box(.38f, .5f, .28f, .18f, .03f), Orange, OrangeDark, .03f);
                    c.Rimmed(Box(.78f, .44f, .12f, .12f, .04f), Blue, BlueDark, .03f);
                    c.Fill(Box(.8f, .5f, .06f, .05f, .01f), 0x9ED9F6FF);
                    c.Rimmed(Disc(.28f, .26f, .1f), Black, 0x000000FF, .02f);
                    c.Rimmed(Disc(.74f, .26f, .1f), Black, 0x000000FF, .02f);
                    c.Fill(Disc(.28f, .26f, .04f), Gray);
                    c.Fill(Disc(.74f, .26f, .04f), Gray);
                    return true;
                case "xp":
                    c.Rimmed(Round(Star(.5f, .5f, .44f, .26f, 6), .04f), Blue, BlueDark, .04f);
                    c.Fill(Round(Star(.5f, .5f, .2f, .09f, 5), .01f), White);
                    return true;
                case "people":
                    c.Rimmed(Disc(.5f, .68f, .15f), 0xF2C79AFF, 0xB07A4AFF, .025f);
                    c.Rimmed(Ellipse(.5f, .26f, .28f, .2f), Green, GreenDark, .03f);
                    return true;
                case "lot":
                    c.Rimmed(Poly(.08f, .3f, .5f, .12f, .92f, .3f, .5f, .48f), Grass, GreenDark, .03f);
                    c.Fill(Line(.5f, .3f, .5f, .8f, .03f), BrownDark);
                    c.Rimmed(Box(.62f, .72f, .14f, .09f, .02f), Cream, CreamDark, .02f);
                    return true;
                case "beauty":
                    c.Fill(Line(.5f, .1f, .5f, .5f, .03f), GreenDark);
                    c.Rimmed(Ellipse(.36f, .26f, .1f, .05f), Green, GreenDark, .015f);
                    c.Rimmed(Round(Star(.5f, .62f, .3f, .14f, 5), .06f), Pink, PinkDark, .03f);
                    c.Rimmed(Disc(.5f, .62f, .09f), Gold, GoldDark, .02f);
                    return true;
                case "soil":
                    c.Rimmed(Ellipse(.5f, .36f, .4f, .2f), Brown, BrownDark, .03f);
                    c.Fill(Ellipse(.36f, .42f, .06f, .025f), BrownDark);
                    c.Fill(Ellipse(.62f, .34f, .06f, .025f), BrownDark);
                    c.Fill(Line(.5f, .5f, .5f, .64f, .02f), GreenDark);
                    c.Rimmed(Ellipse(.44f, .66f, .06f, .03f), Green, GreenDark, .012f);
                    return true;
                case "plus":
                    c.Rimmed(Disc(.5f, .5f, .4f), Green, GreenDark, .04f);
                    c.Fill(Union(Box(.5f, .5f, .22f, .06f, .03f), Box(.5f, .5f, .06f, .22f, .03f)), White);
                    return true;
                case "clock":
                    c.Rimmed(Disc(.5f, .5f, .38f), White, GrayDark, .04f);
                    c.Fill(Line(.5f, .5f, .5f, .74f, .03f), Outline);
                    c.Fill(Line(.5f, .5f, .68f, .44f, .03f), Outline);
                    c.Fill(Disc(.5f, .5f, .04f), Red);
                    return true;
            }
            return false;
        }

        const uint Grass = 0x86C95AFF;

        // ------------------------------------------------------------------ goods

        static bool Item(Canvas c, string id)
        {
            switch (id)
            {
                case "wheat": Ears(c, Gold, GoldDark); return true;
                case "sunflower":
                    c.Rimmed(Round(Star(.5f, .52f, .42f, .26f, 12), .02f), Gold, GoldDark, .02f);
                    c.Rimmed(Disc(.5f, .52f, .18f), 0x7A4A1EFF, BrownDark, .02f);
                    return true;
                case "strawberry":
                    c.Rimmed(Round(Poly(.2f, .64f, .8f, .64f, .5f, .1f), .1f), Red, RedDark, .03f);
                    c.Fill(Round(Star(.5f, .72f, .18f, .07f, 5), .01f), Green);
                    for (int i = 0; i < 3; i++)
                        c.Fill(Disc(.38f + i * .12f, .46f - (i % 2) * .08f, .018f), Gold);
                    return true;
                case "cotton": Puffs(c, White, Gray); c.Fill(Line(.5f, .1f, .5f, .3f, .03f), Brown); return true;
                case "beet":
                    c.Rimmed(Ellipse(.5f, .42f, .26f, .28f), 0x9C2A5AFF, 0x5E1434FF, .03f);
                    c.Fill(Line(.5f, .14f, .5f, .04f, .02f), 0x5E1434FF);
                    Leaves(c, .5f, .72f);
                    return true;
                case "potato":
                    c.Rimmed(Ellipse(.5f, .46f, .36f, .26f), 0xD8A866FF, 0x9A6A32FF, .03f);
                    c.Fill(Disc(.4f, .5f, .025f), 0x9A6A32FF);
                    c.Fill(Disc(.6f, .4f, .025f), 0x9A6A32FF);
                    c.Fill(Disc(.56f, .58f, .02f), 0x9A6A32FF);
                    return true;
                case "pumpkin":
                    c.Rimmed(Union(Ellipse(.34f, .42f, .2f, .26f), Ellipse(.66f, .42f, .2f, .26f), Ellipse(.5f, .42f, .2f, .28f)), Orange, OrangeDark, .03f);
                    c.Fill(Line(.5f, .2f, .5f, .64f, .012f), OrangeDark);
                    c.Fill(Line(.5f, .7f, .56f, .84f, .035f), GreenDark);
                    return true;
                case "pear":
                    c.Fill(Line(.5f, .76f, .54f, .9f, .025f), BrownDark);
                    c.Rimmed(Union(Disc(.5f, .34f, .26f), Disc(.5f, .6f, .16f)), 0xC8D84AFF, 0x7A8A1EFF, .035f);
                    c.Fill(Ellipse(.42f, .4f, .06f, .04f), Shine);
                    return true;
                case "cherry":
                    c.Fill(Line(.34f, .4f, .56f, .86f, .02f), GreenDark);
                    c.Fill(Line(.66f, .36f, .56f, .86f, .02f), GreenDark);
                    c.Rimmed(Disc(.34f, .34f, .17f), 0xC8102EFF, 0x7A0A1CFF, .03f);
                    c.Rimmed(Disc(.66f, .3f, .17f), 0xC8102EFF, 0x7A0A1CFF, .03f);
                    c.Fill(Ellipse(.3f, .4f, .04f, .03f), Shine);
                    return true;
                case "egg":
                    c.Rimmed(Ellipse(.5f, .46f, .26f, .34f), Cream, CreamDark, .03f);
                    c.Fill(Ellipse(.42f, .58f, .06f, .08f), Shine);
                    return true;
                case "milk": Bottle(c, White, Blue); return true;
                case "wool": Puffs(c, Cream, CreamDark); return true;
                case "honey": Jar(c, Gold, GoldDark); return true;
                case "truffle":
                    c.Rimmed(Union(Disc(.42f, .42f, .22f), Disc(.6f, .5f, .2f), Disc(.5f, .3f, .18f)), 0x5A3A2AFF, 0x2E1A10FF, .03f);
                    c.Fill(Disc(.46f, .48f, .03f), 0x8A6A4AFF);
                    return true;
                case "chicken_feed": Sack(c, Cream, Gold); return true;
                case "cow_feed": Sack(c, Cream, Green); return true;
                case "sheep_feed": Sack(c, Cream, Blue); return true;
                case "pig_feed": Sack(c, Cream, Pink); return true;
                case "flour": Sack(c, White, Gray); return true;
                case "bread": Loaf(c, 0xD9934AFF, 0x8A5422FF); return true;
                case "corn_bread": Loaf(c, 0xF0C040FF, 0xB07A10FF); return true;
                case "apple_pie": Pie(c, 0x86C95AFF); return true;
                case "pumpkin_pie": Pie(c, Orange); return true;
                case "honey_cake":
                    for (int i = 0; i < 3; i++)
                        c.Rimmed(Box(.5f, .26f + i * .16f, .34f, .07f, .03f), i % 2 == 0 ? 0xD9934AFF : Cream, 0x8A5422FF, .02f);
                    c.Fill(Ellipse(.5f, .68f, .3f, .05f), Gold);
                    return true;
                case "cookies":
                    c.Rimmed(Disc(.38f, .4f, .2f), 0xD9A066FF, 0x8A5422FF, .03f);
                    c.Rimmed(Disc(.62f, .56f, .2f), 0xD9A066FF, 0x8A5422FF, .03f);
                    c.Fill(Disc(.6f, .6f, .03f), BrownDark);
                    c.Fill(Disc(.66f, .5f, .03f), BrownDark);
                    c.Fill(Disc(.36f, .44f, .03f), BrownDark);
                    return true;
                case "cream": Jar(c, White, Gray); return true;
                case "butter":
                    c.Rimmed(Poly(.12f, .3f, .7f, .3f, .88f, .5f, .3f, .5f), 0xFFE680FF, 0xD9B020FF, .03f);
                    c.Rimmed(Poly(.12f, .3f, .3f, .5f, .3f, .7f, .12f, .5f), 0xFFF0A0FF, 0xD9B020FF, .03f);
                    c.Rimmed(Poly(.3f, .5f, .88f, .5f, .88f, .7f, .3f, .7f), 0xFFF4B8FF, 0xD9B020FF, .03f);
                    return true;
                case "cheese":
                    c.Rimmed(Poly(.12f, .28f, .88f, .28f, .88f, .56f, .12f, .44f), Gold, GoldDark, .03f);
                    c.Fill(Disc(.4f, .38f, .05f), GoldDark);
                    c.Fill(Disc(.68f, .42f, .04f), GoldDark);
                    return true;
                case "strawberry_jam": Jar(c, Red, RedDark); return true;
                case "apple_jam": Jar(c, 0x9ED04AFF, GreenDark); return true;
                case "pear_jam": Jar(c, 0xD8D84AFF, 0x8A8A1EFF); return true;
                case "cherry_jam": Jar(c, 0xA0102EFF, 0x5A0A1CFF); return true;
                case "fabric":
                    c.Rimmed(Box(.5f, .44f, .36f, .2f, .06f), Blue, BlueDark, .03f);
                    c.Fill(Line(.24f, .44f, .76f, .44f, .015f), White);
                    c.Rimmed(Disc(.78f, .44f, .12f), 0x6AB8F0FF, BlueDark, .02f);
                    return true;
                case "yarn":
                    c.Rimmed(Disc(.5f, .46f, .32f), Pink, PinkDark, .03f);
                    c.Fill(Ring(.5f, .46f, .18f, .02f), PinkDark);
                    c.Fill(Line(.3f, .3f, .7f, .62f, .015f), PinkDark);
                    c.Fill(Line(.76f, .3f, .92f, .14f, .015f), PinkDark);
                    return true;
                case "sweater":
                    c.Rimmed(Poly(.3f, .8f, .7f, .8f, .92f, .6f, .82f, .48f, .72f, .56f, .72f, .14f, .28f, .14f, .28f, .56f, .18f, .48f, .08f, .6f),
                        Purple, PurpleDark, .03f);
                    c.Fill(Line(.32f, .3f, .68f, .3f, .02f), White);
                    return true;
                case "sugar":
                    c.Rimmed(Box(.36f, .34f, .16f, .16f, .03f), White, Gray, .03f);
                    c.Rimmed(Box(.64f, .34f, .16f, .16f, .03f), White, Gray, .03f);
                    c.Rimmed(Box(.5f, .62f, .16f, .14f, .03f), White, Gray, .03f);
                    return true;
                case "syrup": Bottle(c, 0xE09A30FF, 0xA0601AFF); return true;
                case "candy":
                    c.Fill(Line(.5f, .1f, .5f, .5f, .03f), White);
                    c.Rimmed(Disc(.5f, .62f, .26f), Red, RedDark, .03f);
                    c.Fill(Ring(.5f, .62f, .13f, .03f), White);
                    return true;
            }
            return false;
        }

        static void Ears(Canvas c, uint face, uint rim)
        {
            for (int k = -1; k <= 1; k++)
            {
                float x = .5f + k * .16f;
                c.Fill(Line(.5f, .08f, x, .56f, .02f), rim);
                for (int i = 0; i < 4; i++)
                    c.Rimmed(Ellipse(x + (i % 2 == 0 ? -.04f : .04f), .6f + i * .08f, .045f, .065f), face, rim, .015f);
            }
        }

        static void Leaves(Canvas c, float x, float y)
        {
            c.Rimmed(Ellipse(x - .1f, y + .06f, .08f, .14f), Green, GreenDark, .015f);
            c.Rimmed(Ellipse(x + .1f, y + .06f, .08f, .14f), Green, GreenDark, .015f);
        }

        static void Puffs(Canvas c, uint face, uint rim) =>
            c.Rimmed(Union(Disc(.34f, .48f, .18f), Disc(.66f, .48f, .18f), Disc(.5f, .64f, .2f), Disc(.5f, .38f, .18f)), face, rim, .03f);

        static void Sack(Canvas c, uint face, uint band)
        {
            c.Rimmed(Round(Poly(.24f, .1f, .76f, .1f, .82f, .52f, .62f, .72f, .38f, .72f, .18f, .52f), .06f), face, CreamDark, .03f);
            c.Rimmed(Box(.5f, .78f, .12f, .06f, .03f), face, CreamDark, .02f);
            c.Fill(Box(.5f, .36f, .26f, .07f, .02f), band);
        }

        static void Jar(Canvas c, uint fill, uint rim)
        {
            c.Rimmed(Box(.5f, .4f, .28f, .3f, .08f), fill, rim, .03f);
            c.Rimmed(Box(.5f, .78f, .3f, .07f, .03f), 0xE8E2D6FF, GrayDark, .02f);
            c.Fill(Box(.5f, .4f, .16f, .1f, .02f), Cream);
            c.Fill(Ellipse(.32f, .5f, .04f, .1f), Shine);
        }

        static void Bottle(Canvas c, uint fill, uint cap)
        {
            c.Rimmed(Union(Box(.5f, .34f, .22f, .26f, .08f), Box(.5f, .7f, .1f, .14f, .03f)), fill, GrayDark, .03f);
            c.Rimmed(Box(.5f, .86f, .12f, .04f, .02f), cap, GrayDark, .015f);
            c.Fill(Box(.5f, .3f, .22f, .08f), cap);
        }

        static void Loaf(Canvas c, uint face, uint rim)
        {
            c.Rimmed(Union(Ellipse(.5f, .44f, .38f, .22f), Box(.5f, .32f, .38f, .1f, .04f)), face, rim, .03f);
            for (int i = 0; i < 3; i++)
                c.Fill(Line(.32f + i * .16f, .48f, .4f + i * .16f, .6f, .018f), rim);
        }

        static void Pie(Canvas c, uint filling)
        {
            c.Rimmed(Ellipse(.5f, .36f, .4f, .18f), 0xD9934AFF, 0x8A5422FF, .03f);
            c.Fill(Ellipse(.5f, .44f, .34f, .14f), filling);
            for (int i = 0; i < 3; i++)
                c.Fill(Line(.24f + i * .2f, .38f, .34f + i * .2f, .52f, .02f), 0xF0C07AFF);
        }

        // ------------------------------------------------------------------ tools

        static bool Tool(Canvas c, string id)
        {
            switch (id)
            {
                case "hammer":
                    c.Rimmed(Line(.3f, .14f, .58f, .6f, .045f), Brown, BrownDark, .02f);
                    c.Rimmed(Box(.62f, .68f, .22f, .09f, .03f), Gray, GrayDark, .03f);
                    return true;
                case "nails":
                    for (int i = 0; i < 3; i++)
                    {
                        float x = .3f + i * .2f;
                        c.Rimmed(Line(x, .16f, x, .7f, .025f), Gray, GrayDark, .015f);
                        c.Rimmed(Box(x, .74f, .07f, .025f, .01f), Gray, GrayDark, .015f);
                    }
                    return true;
                case "paint":
                    c.Rimmed(Poly(.24f, .7f, .76f, .7f, .7f, .14f, .3f, .14f), Gray, GrayDark, .03f);
                    c.Fill(Ellipse(.5f, .7f, .26f, .07f), Blue);
                    c.Fill(Ring(.5f, .74f, .2f, .015f), GrayDark);
                    c.Fill(Box(.5f, .4f, .2f, .08f, .02f), Blue);
                    return true;
                case "saw":
                    c.Rimmed(Poly(.12f, .4f, .74f, .62f, .74f, .46f, .16f, .26f), Gray, GrayDark, .025f);
                    c.Rimmed(Box(.82f, .56f, .1f, .14f, .04f), Red, RedDark, .025f);
                    return true;
                case "axe":
                    c.Rimmed(Line(.28f, .12f, .62f, .8f, .04f), Brown, BrownDark, .02f);
                    c.Rimmed(Poly(.5f, .6f, .82f, .76f, .86f, .46f, .58f, .48f), Gray, GrayDark, .025f);
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ animals

        static bool Animal(Canvas c, string id)
        {
            switch (id)
            {
                case "chicken":
                    c.Fill(Line(.44f, .14f, .44f, .26f, .02f), Orange);
                    c.Fill(Line(.58f, .14f, .58f, .26f, .02f), Orange);
                    c.Rimmed(Ellipse(.5f, .42f, .3f, .22f), White, GrayDark, .03f);
                    c.Rimmed(Disc(.68f, .64f, .14f), White, GrayDark, .03f);
                    c.Fill(Round(Star(.68f, .8f, .08f, .04f, 3), .01f), Red);
                    c.Fill(Poly(.8f, .66f, .9f, .62f, .8f, .58f), Orange);
                    c.Fill(Disc(.71f, .67f, .025f), Black);
                    return true;
                case "cow":
                    c.Rimmed(Ellipse(.44f, .42f, .32f, .2f), White, GrayDark, .03f);
                    c.Fill(Ellipse(.36f, .46f, .09f, .07f), Black);
                    c.Fill(Ellipse(.52f, .36f, .07f, .05f), Black);
                    c.Rimmed(Disc(.74f, .6f, .15f), White, GrayDark, .03f);
                    c.Rimmed(Ellipse(.78f, .52f, .1f, .06f), Pink, PinkDark, .02f);
                    c.Fill(Line(.66f, .74f, .62f, .84f, .02f), CreamDark);
                    c.Fill(Line(.82f, .74f, .86f, .84f, .02f), CreamDark);
                    return true;
                case "sheep":
                    Puffs(c, White, Gray);
                    c.Rimmed(Ellipse(.74f, .54f, .12f, .14f), Black, 0x000000FF, .02f);
                    c.Fill(Disc(.77f, .58f, .02f), White);
                    return true;
                case "bee":
                    c.Rimmed(Ellipse(.42f, .7f, .14f, .1f), 0xFFFFFFC0, GrayDark, .02f);
                    c.Rimmed(Ellipse(.6f, .72f, .14f, .1f), 0xFFFFFFC0, GrayDark, .02f);
                    c.Rimmed(Ellipse(.5f, .44f, .3f, .2f), Gold, GoldDark, .03f);
                    c.Fill(Box(.44f, .44f, .04f, .18f), Black);
                    c.Fill(Box(.6f, .44f, .04f, .16f), Black);
                    return true;
                case "pig":
                    c.Rimmed(Disc(.5f, .48f, .34f), Pink, PinkDark, .03f);
                    c.Rimmed(Ellipse(.5f, .4f, .13f, .09f), 0xFFB8C8FF, PinkDark, .02f);
                    c.Fill(Disc(.45f, .4f, .025f), PinkDark);
                    c.Fill(Disc(.55f, .4f, .025f), PinkDark);
                    c.Fill(Disc(.38f, .58f, .03f), Black);
                    c.Fill(Disc(.62f, .58f, .03f), Black);
                    return true;
            }
            return false;
        }
    }
}

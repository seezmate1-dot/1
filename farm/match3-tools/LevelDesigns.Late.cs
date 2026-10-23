using System;
using System.Collections.Generic;
using System.Linq;
using SunnyValley.Core.Match3;

namespace SunnyValley.Tools
{
    /// <summary>
    /// Levels 61–200. Every mechanic is known by 60; from 62 on, later variants of them come one at a time,
    /// each with a teaching level first (aimed ~10 points easier than the curve): three portal tiers (62),
    /// honey on grass (66), weed on grass (72), a row of baskets (76), nests for chicks (82), tough beds (86),
    /// stones on grass (92), specials in chains (96), four goals (102), few generators (106), portals with
    /// walls (112), honey and weed together (116), islands (122), specials in ice (126), a stone maze (132),
    /// baskets in honey (142), chicks among beds (152). Boards are drawn from a set of shapes with obstacles
    /// laid over them (<see cref="M"/>); 6 crops on most levels.
    /// </summary>
    public static partial class LevelDesigns
    {
        /// <summary>
        /// Waves of ten: k = 1 easy (90%), k = 2…9 from <c>start</c> down to <c>end</c>, k = 0 hard (42%).
        /// Both ends slide down wave by wave so that the middle of the wave reaches ~55% at 191–199.
        /// </summary>
        static double LateTarget(int id)
        {
            int k = id % 10, wave = id / 10; // wave 6 = 61–69 … 19 = 191–199
            if (k == 1) return 0.90;
            double start = 0.80 - (wave - 5) * 0.0125;
            double end = 0.57 - (wave - 5) * 0.006;
            return start + (end - start) * (k - 1) / 8.0;
        }

        /// <summary>A teaching level: the new idea is shown on a board ~10 points easier than the curve.</summary>
        static Design Teach(Design d, string intro)
        {
            d.Intro = intro;
            d.Target = Math.Min(0.86, d.Target + 0.10);
            return d;
        }

        // ---------------------------------------------------------------- board shapes ('#' cell, ' ' hole)

        static string[] Rep(string row, int n) => Enumerable.Repeat(row, n).ToArray();
        static string[] Cat(params string[][] parts) => parts.SelectMany(p => p).ToArray();

        static readonly string[] Full = Rep("#########", 9);
        static readonly string[] Wide = Rep("#########", 7);
        static readonly string[] Tall = Rep("#######", 9);
        static readonly string[] Oct = Cat(new[] { " ####### " }, Rep("#########", 7), new[] { " ####### " });
        static readonly string[] Diamond = Cat(new[] { "  #####  ", " ####### " }, Rep("#########", 5), new[] { " ####### ", "  #####  " });
        static readonly string[] Ring = Cat(Rep("#########", 3), new[] { "#### ####", "###   ###", "#### ####" }, Rep("#########", 3));
        static readonly string[] Hourglass = { "#########", "#########", " ####### ", "  #####  ", "   ###   ", "  #####  ", " ####### ", "#########", "#########" };
        static readonly string[] Twin = Rep("#### ####", 9);
        static readonly string[] Arch = Cat(Rep("#########", 5), Rep("###   ###", 4));
        static readonly string[] Cross = Cat(Rep("   ###   ", 2), Rep("#########", 5), Rep("   ###   ", 2));
        static readonly string[] Stairs = Cat(new[] { "####     ", "#####    ", "######   ", "#######  ", "######## " }, Rep("#########", 4));
        static readonly string[] Tee = Cat(Rep("#########", 3), Rep("  #####  ", 6));
        static readonly string[] U = Cat(Rep("###   ###", 3), Rep("#########", 6));
        static readonly string[] Heart = { " ##   ## ", "#### ####", "#########", "#########", "#########", " ####### ", "  #####  ", "   ###   ", "    #    " };
        static readonly string[] Pillars = { "#########", "#########", "#########", "## # # ##", "#########", "## # # ##", "#########", "#########", "#########" };
        /// <summary>Only three generators on top (see <see cref="FunnelSpawners"/>); the rest is fed sideways.</summary>
        static readonly string[] Funnel = Cat(new[] { " #  #  # " }, Rep("#########", 8));
        /// <summary>Two tiers: top-left 5×4 falls through portals into bottom-right 5×4 (<see cref="P2"/>).</summary>
        static readonly string[] Portal2 = Cat(Rep("#####    ", 4), new[] { "         " }, Rep("    #####", 4));
        /// <summary>Three tiers zigzagging left → right → left (<see cref="P3"/>).</summary>
        static readonly string[] Portal3 = { "######   ", "######   ", "         ", "   ######", "   ######", "         ", "######   ", "######   ", "######   " };
        /// <summary>Three islands: left and right on top, one in the middle below.</summary>
        static readonly string[] Islands = Cat(Rep("###   ###", 4), Rep("   ###   ", 5));

        static List<int[]> FunnelSpawners() => new List<int[]> { new[] { 1, 0 }, new[] { 4, 0 }, new[] { 7, 0 } };

        static List<PortalData> P2()
        {
            var list = new List<PortalData>();
            for (int x = 0; x < 5; x++) list.Add(Portal(x, 3, x + 4, 5));
            return list;
        }

        static List<PortalData> P3()
        {
            var list = new List<PortalData>();
            for (int x = 0; x < 6; x++) list.Add(Portal(x, 1, x + 3, 3));
            for (int x = 3; x < 9; x++) list.Add(Portal(x, 4, x - 3, 6));
            return list;
        }

        /// <summary>
        /// Lays obstacle rows over a shape, starting at row <paramref name="row"/>: '.' keeps the shape's char,
        /// anything else replaces it (' ' digs a hole). Obstacles may only go on cells.
        /// </summary>
        static string[] M(string[] shape, int row, params string[] rows)
        {
            var map = (string[])shape.Clone();
            for (int r = 0; r < rows.Length; r++)
            {
                int y = row + r;
                var src = map[y].ToCharArray();
                if (rows[r].Length != src.Length) throw new FormatException("overlay row " + y + " has " + rows[r].Length + " chars, the shape " + src.Length);
                for (int x = 0; x < src.Length; x++)
                {
                    char c = rows[r][x];
                    if (c == '.') continue;
                    if (src[x] == ' ' && c != ' ') throw new FormatException("overlay (" + x + "," + y + ") '" + c + "' on a hole");
                    src[x] = c;
                }
                map[y] = new string(src);
            }
            return map;
        }

        static List<WallData> WallsRight(int y0, int y1, params int[] xs)
        {
            var list = new List<WallData>();
            foreach (int x in xs)
                for (int y = y0; y <= y1; y++) list.Add(Wall(x, y, "right"));
            return list;
        }

        static List<int[]> Cells(params (int x, int y)[] cells) => cells.Select(c => new[] { c.x, c.y }).ToList();

        static Design Chicks(this Design d, int spawn, int maxOnBoard, int gap)
        {
            d.Chicks = new ChickRules { Spawn = spawn, MaxOnBoard = maxOnBoard, Gap = gap };
            return d;
        }

        static Design Basket(this Design d, string color, int count)
        {
            d.Baskets.Add((color, count));
            return d;
        }

        static Design With(this Design d, Action<Design> set)
        {
            set(d);
            return d;
        }

        static void AddLate(List<Design> L)
        {
            // ---------------------------------------------------------------- 61–70
            L.Add(D(61, "Утро после праздника", 5, M(Oct, 3, "..ggggg..", "..ggggg..")).Goal("grass").Collect("corn", 15));
            L.Add(Teach(D(62, "Три яруса", 5, M(Portal3, 4, "...gggggg", ".........", "gggggg...", "gggggg...", "gggggg...")), "portal3")
                .Goal("grass").With(d => d.Portals = P3()));
            L.Add(D(63, "Сладкий луг", 6, M(Diamond, 3, ".GGGGGGG.", "GGhGGGhGG", ".GGGGGGG.")).Goal("grass").Goal("honey"));
            L.Add(D(64, "Ящики у колодца", 6, M(Ring, 2, "..11111..", "..1...1..", "..2...2..", "..1...1..", "..11111..")).Goal("crate").Collect("tomato", 15));
            L.Add(D(65, "Цыплята на тропинке", 6, M(Oct, 0, ".k..k..k.", ".........", ".........", ".........", ".........", "iiiiiiiii"))
                .Goal("chick", 4).Collect("carrot", 10).Chicks(1, 3, 3));
            L.Add(Teach(D(66, "Мёд на траве", 5, M(Full, 3, "...mmm...", "..mmmmm..", "...mmm...")), "honeyGrass").Goal("honey").Goal("grass"));
            L.Add(D(67, "Сорняк в загоне", 6, M(Twin, 3, ".w.....w.", ".........", ".........", "1111.1111")).Goal("weed").Goal("crate").Collect("apple", 12));
            L.Add(D(68, "Цепная ограда", 6, M(Arch, 2, "lllllllll", "ll.....ll")).Goal("chain").Collect("eggplant", 15));
            L.Add(D(69, "Каменный ручей", 6, M(Stairs, 4, "s...s....", ".s...s...", "..s...s..", ".........", "ggggggggg")).Goal("stone").Goal("grass"));
            L.Add(D(70, "Ярмарочная площадь", 6, M(Oct, 2, "ggggggggg", "g2b2g2b2g", "ggggggggg", ".........", ".n.n.n.n.")).Goal("grass").Goal("crate").Goal("crop", 4));

            // ---------------------------------------------------------------- 71–80
            L.Add(D(71, "Ласточки", 5, M(Tall, 3, "..ggg..", "..ggg..")).Goal("grass").Collect("tomato", 15));
            L.Add(Teach(D(72, "Сорняк на траве", 5, M(Full, 3, "ggggggggg", "ggvgggvgg", "ggggggggg")), "weedGrass").Goal("weed").Goal("grass"));
            L.Add(D(73, "Лёд за порталом", 5, M(Portal2, 6, "....iiiii", "....i.i.i")).Goal("ice").Collect("apple", 12).With(d => d.Portals = P2()));
            L.Add(D(74, "Пчелиный двор", 6, M(Cross, 3, "....h....", "...hhh...", "....h....")).Goal("honey").Collect("corn", 15));
            L.Add(D(75, "Грядки у ручья", 6, M(U, 4, ".b..1..b.", ".........", "....b....", "...111...")).Goal("crop", 5).Goal("crate"));
            L.Add(Teach(D(76, "Корзины-запруда", 5, M(Full, 4, "u#u#u#u#u")), "basketsRow").Goal("basket")
                .Basket("tomato", 4).Basket("corn", 4).Basket("apple", 4).Basket("carrot", 4).Basket("eggplant", 4));
            L.Add(D(77, "Ледяные камни", 6, M(Oct, 3, ".IsIsIsI.", ".iiiiiii.")).Goal("ice").Goal("stone"));
            L.Add(D(78, "Цыплята за изгородью", 6, M(Full, 0, ".k..k..k.")).Goal("chick", 4).Chicks(1, 3, 2).With(d => d.Walls = WallsRight(0, 4, 2, 5)));
            L.Add(D(79, "Двойная трава", 6, M(Heart, 2, "GGGGGGGGG", "GGGGGGGGG", "GGGGGGGGG", ".GGGGGGG.")).Goal("grass").Collect("blueberry", 12));
            L.Add(D(80, "Заросшая ферма", 6, M(Ring, 2, "ggwgggwgg", "gg.....gg", "GG.....GG", "gg.....gg", "ggwgggwgg")).Goal("grass").Goal("weed").Collect("tomato", 10));

            // ---------------------------------------------------------------- 81–90
            L.Add(D(81, "Тихая заводь", 5, M(Diamond, 3, "...ggg...", "...ggg...", "...ggg...")).Goal("grass").Collect("carrot", 15));
            L.Add(Teach(D(82, "Гнёзда", 5, M(Full, 0, ".k.....k.")), "chickExit").Goal("chick", 3).Chicks(1, 2, 3)
                .With(d => d.ChickExits = Cells((1, 8), (4, 8), (7, 8))));
            L.Add(D(83, "Мёд в ящиках", 6, M(Full, 3, "..12121..", "..1h.h1..", "..12121..")).Goal("honey").Goal("crate"));
            L.Add(D(84, "Цепи у порталов", 5, M(Portal2, 1, "lLlLl....")).Goal("chain").Collect("apple", 15).With(d => d.Portals = P2()));
            L.Add(D(85, "Сорняк и лёд", 6, M(Pillars, 2, "iiiiiiiii", ".........", "....w....")).Goal("weed").Goal("ice").Collect("tomato", 10));
            L.Add(Teach(D(86, "Тугие грядки", 5, M(Full, 3, "..B...B..", ".........", "....B....")), "bed3").Goal("crop", 3));
            L.Add(D(87, "Корзины у моста", 5, M(Twin, 3, "..u...u..", ".........", "1.1...1.1")).Goal("basket").Goal("crate").Basket("tomato", 5).Basket("eggplant", 5));
            L.Add(D(88, "Цыплята в инее", 6, M(Full, 0, "..k...k..", ".........", ".........", ".........", "iiiIIIiii")).Goal("chick", 4).Goal("ice").Chicks(2, 3, 2));
            L.Add(D(89, "Огород в цепях", 6, M(Arch, 2, ".lbl.lbl.", "lllllllll")).Goal("crop", 4).Goal("chain"));
            L.Add(D(90, "Медовая буря", 6, M(Full, 2, "GGGGGGGGG", "GmGGGGGmG", "GGGGmGGGG", "GmGGGGGmG", "GGGGGGGGG")).Goal("honey").Goal("grass").Collect("corn", 10));

            // ---------------------------------------------------------------- 91–100
            L.Add(D(91, "Яблоневый сад", 5, Oct).Collect("apple", 20).Collect("tomato", 12));
            L.Add(Teach(D(92, "Камни в траве", 5, M(Full, 4, "gtgggggtg", "ggggtgggg", "gtgggggtg")), "stoneGrass").Goal("stone").Goal("grass"));
            L.Add(D(93, "Ледяной мост", 5, M(Portal3, 3, "...ii..ii")).Goal("ice").Collect("tomato", 12).With(d => d.Portals = P3()));
            L.Add(D(94, "Сорняк у корзин", 6, M(Diamond, 2, "....w....", ".........", "..u...u..")).Goal("basket").Goal("weed")
                .Basket("carrot", 5).Basket("eggplant", 5));
            L.Add(D(95, "Ящичный лабиринт", 6, M(Stairs, 4, "1.2.3.2..", ".........", ".3.2.1.2.", "..1...1..")).Goal("crate").Collect("corn", 12));
            L.Add(Teach(D(96, "Скованные вилы", 5, M(Full, 4, "lllllllll")), "chainedSpecials").Goal("chain").Collect("carrot", 20)
                .With(d => d.Specials = new List<SpecialData> { Sp(2, 4, "lineV", "tomato"), Sp(4, 4, "bomb", "corn"), Sp(6, 4, "lineV", "apple") }));
            L.Add(D(97, "Цыплята и мёд", 6, M(U, 0, ".k.....k.", ".........", ".........", ".........", ".........", "h.h.h.h.h")).Goal("chick", 4).Goal("honey").Chicks(2, 3, 2));
            L.Add(D(98, "Трава под ящиками", 6, M(Ring, 2, "ggqqqqqgg", "ggq...qgg", "ggg...ggg", "ggq...qgg", "ggqqqqqgg")).Goal("grass").Goal("crate"));
            L.Add(D(99, "Камни и грядки", 6, M(Cross, 3, ".s.b.b.s.", "....s....", ".s.....s.")).Goal("crop", 4).Goal("stone"));
            L.Add(D(100, "Сотый урожай", 6, M(Oct, 2, "GGGGGGGGG", "G1b1G1b1G", "GGGGGGGGG", ".........", ".2.2.2.2.")).Goal("grass").Goal("crate").Goal("crop", 4));

            // ---------------------------------------------------------------- 101–110
            L.Add(D(101, "Отдых у пруда", 5, Wide).Collect("tomato", 15).Collect("corn", 15));
            L.Add(Teach(D(102, "Четыре дела", 6, M(Full, 3, "..11111..", ".........", "iiiiiiiii", ".........", "ggggggggg")), "mixed4")
                .Goal("grass").Goal("crate").Goal("ice").Collect("apple", 10));
            L.Add(D(103, "Мёд за порталом", 6, M(Portal2, 1, ".h.h.....", "..h......", ".........", ".........", ".........", "....ggggg", "....ggggg")).Goal("honey").Goal("grass")
                .With(d => d.Portals = P2()));
            L.Add(D(104, "Корзины в цепях", 6, M(Oct, 4, "..u...u..", "...l.l...")).Goal("basket").Goal("chain").Basket("apple", 4).Basket("corn", 4));
            L.Add(D(105, "Цыплята-прыгуны", 6, M(Diamond, 0, "...k.k...")).Goal("chick", 3).Collect("corn", 10).Chicks(1, 3, 2));
            L.Add(Teach(D(106, "Узкий жёлоб", 6, M(Funnel, 7, "ggggggggg", "ggggggggg")), "spawners").Goal("grass").Collect("corn", 15)
                .With(d => d.Spawners = FunnelSpawners()));
            L.Add(D(107, "Сорняк на камнях", 6, M(Heart, 2, ".s..w..s.", ".........", "..s...s..")).Goal("weed").Goal("stone"));
            L.Add(D(108, "Ледяная грядка", 6, M(Twin, 3, ".b.....b.", "iiii.iiii", ".........", "..b...b..")).Goal("crop", 4).Goal("ice"));
            L.Add(D(109, "Тяжёлый лёд", 6, M(Hourglass, 1, "IIIIIIIII", ".iiiiiii.", ".........", ".........", ".........", ".........", "iiiiiiiii")).Goal("ice").Collect("eggplant", 10));
            L.Add(D(110, "Большая стройка", 6, M(Full, 2, "2.2.2.2.2", ".........", ".s.S.S.s.", ".........", "2222.2222")).Goal("crate").Goal("stone").Collect("blueberry", 10));

            // ---------------------------------------------------------------- 111–120
            L.Add(D(111, "Летний дождь", 5, M(Oct, 3, "..ggggg..", "..ggggg..")).Goal("grass").Collect("carrot", 15));
            L.Add(Teach(D(112, "Мост с перилами", 6, M(Portal2, 7, "....ggggg", "....ggggg")), "portalWalls").Goal("grass")
                .With(d => { d.Portals = P2(); d.Walls = WallsRight(5, 8, 5, 7); }));
            L.Add(D(113, "Цыплята под замком", 6, M(Full, 0, "..k...k..", ".........", ".........", ".........", "l.l.l.l.l")).Goal("chick", 3).Goal("chain").Chicks(1, 3, 2));
            L.Add(D(114, "Медовый камень", 6, M(Ring, 2, "..s.h.s..", ".........", ".........", ".........", "..s...s..")).Goal("honey").Goal("stone"));
            L.Add(D(115, "Огород у дороги", 6, M(Stairs, 4, ".B...B...", ".........", ".........", "ggggggggg", "ggggggggg")).Goal("crop", 3).Goal("grass"));
            L.Add(Teach(D(116, "Мёд и сорняк", 6, M(Full, 3, "..h...h..", ".........", ".........", "..w...w..")), "honeyWeed").Goal("honey").Goal("weed"));
            L.Add(D(117, "Ящики на мосту", 5, M(Portal3, 3, "...1.1.1.", ".........", ".........", ".........", "..1..1...")).Goal("crate").Collect("apple", 12).With(d => d.Portals = P3()));
            L.Add(D(118, "Корзины на траве", 6, M(Diamond, 4, "..u...u..", ".ggggggg.", "..ggggg..")).Goal("basket").Goal("grass").Basket("corn", 5).Basket("blueberry", 5));
            L.Add(D(119, "Ледяная цепь", 6, M(Cross, 3, "..iiLii..", "lllllllll", "..iiLii..")).Goal("ice").Goal("chain"));
            L.Add(D(120, "Осенняя страда", 6, M(Oct, 2, "GGGGGGGGG", "GbGvGvGbG", "GGGGGGGGG")).Goal("crop", 4).Goal("weed").Goal("grass"));

            // ---------------------------------------------------------------- 121–130
            L.Add(D(121, "Тёплый ветер", 5, Tall).Collect("corn", 15).Collect("apple", 15));
            L.Add(Teach(D(122, "Острова", 5, M(Islands, 2, "ggg...ggg", "ggg...ggg", ".........", ".........", "...ggg...", "...ggg...")), "islands").Goal("grass"));
            L.Add(D(123, "Цыплята за рекой", 6, M(Twin, 0, ".k....k..", ".........", ".........", ".........", ".........", ".........", ".........", "gggg.gggg"))
                .Goal("chick", 3).Goal("grass").Chicks(1, 3, 2));
            L.Add(D(124, "Каменная гряда", 6, M(Full, 4, "Ss.sSs.sS")).Goal("stone").Collect("tomato", 12));
            L.Add(D(125, "Мёд на мосту", 5, M(Portal3, 3, "....h....", ".........", ".........", ".........", "...h....."))
                .Goal("honey").Collect("carrot", 10).With(d => d.Portals = P3()));
            L.Add(Teach(D(126, "Подарки во льду", 6, M(Full, 3, "iiiiiiiii", "iiIiIiIii", "iiiiiiiii")), "iceSpecial").Goal("ice").Collect("corn", 12)
                .With(d => d.Specials = new List<SpecialData> { Sp(2, 4, "bomb", "tomato"), Sp(4, 4, "rainbow"), Sp(6, 4, "bomb", "apple") }));
            L.Add(D(127, "Сорняк в ящиках", 6, M(Ring, 2, "..1w1w1..", ".........", ".........", ".........", "..1w1w1..")).Goal("weed").Goal("crate").Collect("carrot", 12));
            L.Add(D(128, "Корзины у порталов", 5, M(Portal2, 2, ".u.u.....")).Goal("basket").Basket("tomato", 8).Basket("apple", 8).With(d => d.Portals = P2()));
            L.Add(D(129, "Грядки на траве", 6, M(Heart, 2, "ggbgGgbgg", "gGGGGGGGg", "ggggbgggg")).Goal("crop", 4).Goal("grass"));
            L.Add(D(130, "Ледниковый период", 6, M(Full, 2, "iiiiiiiii", "lsl.s.lsl", "iiiiiiiii")).Goal("ice").Goal("stone").Goal("chain"));

            // ---------------------------------------------------------------- 131–140
            L.Add(D(131, "Пикник", 5, Tee).Collect("apple", 18).Collect("tomato", 18));
            L.Add(Teach(D(132, "Каменный лабиринт", 5, M(Full, 2, "..S...S..", "..s...s..", "..S...S..", "..s...s..", "....s....")), "stoneMaze").Goal("stone"));
            L.Add(D(133, "Мёд в загонах", 6, M(Full, 4, ".h..h..h.")).Goal("honey").Collect("blueberry", 12).With(d => d.Walls = WallsRight(0, 8, 2, 5)));
            L.Add(D(134, "Цыплята у гнёзд", 6, M(Full, 0, ".k..k..k.", ".........", ".........", ".........", ".........", "1.1...1.1")).Goal("chick", 4).Goal("crate").Chicks(1, 3, 3)
                .With(d => d.ChickExits = Cells((1, 8), (4, 8), (7, 8))));
            L.Add(D(135, "Трава в цепях", 6, M(Diamond, 3, ".nnnnnnn.", ".NgNgNgN.", ".nnnnnnn.")).Goal("grass").Goal("chain"));
            L.Add(D(136, "Сорная тропа", 6, M(Stairs, 5, "ggggggggg", "gvgggggvg")).Goal("weed").Goal("grass").Collect("tomato", 10));
            L.Add(D(137, "Корзина-крепость", 6, M(Full, 3, "...111...", "...1u1...", "...111...")).Goal("basket").Goal("crate").Basket("eggplant", 6));
            L.Add(D(138, "Три моста", 6, M(Portal3, 0, ".k..k....", ".........", ".........", ".........", ".........", ".........", ".........", "gggggg...", "gggggg..."))
                .Goal("chick", 3).Goal("grass").Chicks(1, 2, 3).With(d => { d.Portals = P3(); d.ChickExits = Cells((0, 8), (1, 8), (2, 8), (3, 8), (4, 8), (5, 8)); }));
            L.Add(D(139, "Лёд у грядок", 6, M(Cross, 3, "..B...B..", "iiiiiiiii", "IIIIIIIII")).Goal("crop", 3).Goal("ice"));
            L.Add(D(140, "Крепость", 6, M(Ring, 2, "gg11111gg", "gg1...1gg", "ggs...sgg", "gg1...1gg", "gg11111gg")).Goal("crate").Goal("stone").Goal("grass"));

            // ---------------------------------------------------------------- 141–150
            L.Add(D(141, "Полевые цветы", 5, M(Diamond, 4, "..ggggg..")).Goal("grass").Collect("tomato", 15));
            L.Add(Teach(D(142, "Медовые корзины", 5, M(Full, 3, "..h...h..", ".huh.huh.", "..h...h..")), "honeyBaskets").Goal("basket").Goal("honey")
                .Basket("corn", 6).Basket("apple", 6));
            L.Add(D(143, "Ледяные острова", 5, M(Islands, 1, "iii...iii", ".........", ".........", ".........", "...iii...")).Goal("ice").Collect("corn", 10));
            L.Add(D(144, "Цепи на мосту", 5, M(Portal3, 1, "lLlLlL...", ".........", ".........", ".........", ".........", ".........", "l.l.l...."))
                .Goal("chain").Collect("apple", 10).With(d => d.Portals = P3()));
            L.Add(D(145, "Сорняк в траве", 6, M(Oct, 3, "GGGGGGGGG", "GvGGvGGvG", "GGGGGGGGG")).Goal("weed").Goal("grass"));
            L.Add(D(146, "Огород среди камней", 6, M(Full, 2, "ggggggggg", "gbgtgtgbg", "ggggggggg")).Goal("crop", 4).Goal("stone").Goal("grass"));
            L.Add(D(147, "Цыплята в мёду", 6, M(Oct, 0, "..k...k..", ".........", ".........", ".........", "h...h...h")).Goal("chick", 4).Goal("honey").Chicks(2, 3, 2));
            L.Add(D(148, "Склад на холме", 6, M(Stairs, 4, "2.2.2.2..", ".........", "33.333.33")).Goal("crate").Collect("eggplant", 10));
            L.Add(D(149, "Ледяной загон", 6, M(Full, 3, "IIIIIIIII", "iiiiiiiii")).Goal("ice").With(d => d.Walls = WallsRight(0, 8, 2, 5)));
            L.Add(D(150, "Золотая осень", 6, M(Oct, 2, "GGGGGGGGG", "G1h1G1h1G", "GGGGGGGGG")).Goal("grass").Goal("crate").Goal("honey").Collect("tomato", 10));

            // ---------------------------------------------------------------- 151–160
            L.Add(D(151, "Солнечное утро", 6, M(Wide, 3, "..11111..")).Goal("crate").Collect("corn", 15));
            L.Add(Teach(D(152, "Цыплята в огороде", 6, M(Full, 0, ".k.....k.", ".........", ".........", ".........", "..b...b..")), "chickBeds")
                .Goal("chick", 3).Goal("crop", 3).Chicks(1, 2, 3));
            L.Add(D(153, "Острова в траве", 5, M(Islands, 2, "ggg...ggg", "ggg...ggg", ".........", ".........", ".........", "...ggg...", "...ggg...")).Goal("grass").Collect("carrot", 10));
            L.Add(D(154, "Медовый лабиринт", 6, M(Full, 2, "...s.s...", ".h.....h.", "...s.s...")).Goal("honey").Goal("stone").Collect("eggplant", 10));
            L.Add(D(155, "Корзины за стеной", 6, M(Full, 4, ".u.....u.", ".........", "....u....")).Goal("basket").Basket("tomato", 4).Basket("corn", 4).Basket("apple", 3)
                .With(d => d.Walls = WallsRight(0, 8, 3, 4)));
            L.Add(D(156, "Сорняк у моста", 6, M(Portal2, 1, ".w.w.....", ".........", ".........", ".........", ".........", ".........", "....w...w")).Goal("weed").Collect("apple", 10)
                .With(d => d.Portals = P2()));
            L.Add(D(157, "Двойные ящики", 6, M(Heart, 2, "l2l2l2l2l", "2l2l2l2l2")).Goal("crate").Goal("chain"));
            L.Add(D(158, "Ледяные грядки", 6, M(Ring, 2, "ggbgjgbgg", ".........", ".........", ".........", "ggbgjgbgg")).Goal("crop", 4).Goal("ice").Goal("grass"));
            L.Add(D(159, "Цыплята на островах", 5, M(Islands, 0, ".k.....k.", ".........", "i.......i")).Goal("chick", 3).Goal("ice").Chicks(1, 2, 3));
            L.Add(D(160, "Буря", 6, M(Full, 3, "..w...w..", "hh.....hh", ".........", "hh.....hh")).Goal("weed").Goal("honey").Collect("blueberry", 10));

            // ---------------------------------------------------------------- 161–170
            L.Add(D(161, "Вечерняя роса", 6, M(Wide, 3, "...ggg...")).Goal("grass").Collect("apple", 15));
            L.Add(D(162, "Длинный спуск", 5, M(Portal3, 0, "GGGGGG...", "gggggg...", ".........", "...1.1.1.")).Goal("grass").Goal("crate").With(d => d.Portals = P3()));
            L.Add(D(163, "Медовые острова", 5, M(Islands, 6, "....h....")).Goal("honey").Collect("corn", 10));
            L.Add(D(164, "Каменные корзины", 6, M(Full, 4, "..u...u..", ".........", "..s...s..")).Goal("basket").Goal("stone").Basket("carrot", 5).Basket("blueberry", 5));
            L.Add(D(165, "Цыплята в лабиринте", 6, M(Full, 0, "k...k...k")).Goal("chick", 3)
                .With(d => d.Walls = WallsRight(3, 6, 2, 5)));
            L.Add(D(166, "Скованный огород", 6, M(Diamond, 3, ".lbl.lbl.", ".........", "..llbll..")).Goal("crop", 4).Goal("chain"));
            L.Add(D(167, "Лёд и сорняк", 6, M(Twin, 2, "iiii.iiii", ".........", ".........", "......w..", ".........", "iiii.iiii")).Goal("ice").Goal("weed"));
            L.Add(D(168, "Подарки в цепях", 6, M(Cross, 4, "LLLLLLLLL", ".........", "ggggggggg")).Goal("chain").Goal("grass")
                .With(d => d.Specials = new List<SpecialData> { Sp(1, 4, "bee", "corn"), Sp(4, 4, "lineH", "carrot"), Sp(7, 4, "bee", "eggplant") }));
            L.Add(D(169, "Тяжёлые камни", 6, M(Hourglass, 1, ".s.s.s.s.", ".........", ".........", ".........", ".........", ".........", "..s...s..")).Goal("stone").Collect("carrot", 12));
            L.Add(D(170, "Последний сенокос", 6, M(Full, 2, "GGGGGGGGG", "G3G3w3G3G", "GGGGGGGGG")).Goal("grass").Goal("crate").Goal("weed"));

            // ---------------------------------------------------------------- 171–180
            L.Add(D(171, "Ясный день", 6, Diamond).Collect("corn", 15).Collect("apple", 15));
            L.Add(D(172, "Узкие жёлобы", 6, M(Funnel, 5, "iiiiiiiii", "iiiiiiiii")).Goal("ice").Collect("tomato", 10).With(d => d.Spawners = FunnelSpawners()));
            L.Add(D(173, "Мёд у гнёзд", 5, M(Full, 0, "k...k...k", ".........", ".........", ".........", ".........", "..h...h..")).Goal("chick", 3).Goal("honey")
                .With(d => d.ChickExits = Cells((0, 8), (2, 8), (4, 8), (6, 8), (8, 8))));
            L.Add(D(174, "Каменный мост", 5, M(Portal2, 2, ".S.S.....", ".........", ".........", ".........", ".........", "....s.s.s")).Goal("stone").Collect("eggplant", 10)
                .With(d => d.Portals = P2()));
            L.Add(D(175, "Луга на островах", 5, M(Islands, 1, "GGG...GGG", ".........", ".........", ".........", ".........", "...GGG...")).Goal("grass").Collect("apple", 10));
            L.Add(D(176, "Трава за стеной", 6, M(Full, 3, "GGGGGGGGG", "GGGGGGGGG", "GGGGGGGGG")).Goal("grass").With(d => d.Walls = WallsRight(0, 8, 2, 5)));
            L.Add(D(177, "Ящики и лёд", 6, M(Oct, 3, "I2I2I2I2I", "iiiiiiiii")).Goal("crate").Goal("ice"));
            L.Add(D(178, "Огород у портала", 6, M(Portal3, 3, "....b..b.", ".........", ".........", ".........", ".b..b....")).Goal("crop", 4).With(d => d.Portals = P3()));
            L.Add(D(179, "Сорняк в цепях", 6, M(Ring, 2, "..l.w.l..", "ll.....ll", ".........", "ll.....ll", "..l.w.l..")).Goal("weed").Goal("chain").Collect("apple", 10));
            L.Add(D(180, "Великая уборка", 6, M(Full, 2, "GGGGGGGGG", "g1l1g1l1g", "GGGGGGGGG")).Goal("grass").Goal("crate").Goal("chain").Collect("corn", 10));

            // ---------------------------------------------------------------- 181–190
            L.Add(D(181, "Тихий вечер", 6, M(Tall, 4, "..ggg..")).Goal("grass").Collect("carrot", 15));
            L.Add(D(182, "Ледяные цыплята", 6, M(Oct, 0, ".k..k..k.", ".........", ".........", ".iiiiiii.")).Goal("chick", 4).Goal("ice").Chicks(1, 3, 3));
            L.Add(D(183, "Медовый холм", 6, M(Stairs, 3, "gmggmgg..", ".........", ".........", "ggmgggmgg")).Goal("honey").Goal("grass").Collect("eggplant", 12));
            L.Add(D(184, "Корзины и сорняк", 6, M(Oct, 3, "..u...u..", ".........", ".........", "....w....")).Goal("basket").Goal("weed").Basket("tomato", 6).Basket("eggplant", 6));
            L.Add(D(185, "Каменные острова", 5, M(Islands, 1, ".s.....s.", ".........", ".........", ".........", ".........", "...s.s...")).Goal("stone").Collect("apple", 10));
            L.Add(D(186, "Цепной мост", 5, M(Portal2, 1, "lLlLl....", ".........", ".........", ".........", ".........", "....IIIII")).Goal("chain").Goal("ice").With(d => d.Portals = P2()));
            L.Add(D(187, "Грядки за стеной", 6, M(Full, 4, ".B..B..B.")).Goal("crop", 3).With(d => d.Walls = WallsRight(0, 8, 2, 5)));
            L.Add(D(188, "Двойные подарки", 6, M(Heart, 3, "IIIIIIIII", "iiiiiiiii")).Goal("ice").Collect("blueberry", 10)
                .With(d => d.Specials = new List<SpecialData> { Sp(2, 3, "lineH", "corn"), Sp(6, 3, "lineV", "apple") }));
            L.Add(D(189, "Ящики на островах", 5, M(Islands, 2, "1.1...1.1", ".........", ".........", ".........", "...222...")).Goal("crate"));
            L.Add(D(190, "Ледяной шторм", 6, M(Oct, 2, "iiiiiiiii", "lsl.S.lsl", "iiiiiiiii")).Goal("ice").Goal("chain").Goal("stone"));

            // ---------------------------------------------------------------- 191–200
            L.Add(D(191, "Перед праздником", 6, Tee).Collect("tomato", 15).Collect("carrot", 15));
            L.Add(D(192, "Медовые гнёзда", 6, M(Full, 0, "..k...k..", ".........", ".........", ".........", ".........", ".........", "m.......m")).Goal("chick", 3).Goal("honey").Goal("grass")
                .Chicks(1, 3, 2).With(d => d.ChickExits = Cells((1, 8), (2, 8), (6, 8), (7, 8))));
            L.Add(D(193, "Каменный сад", 6, M(Diamond, 3, ".gtgggtg.", "gggtgtggg", ".gtgggtg.")).Goal("stone").Goal("grass"));
            L.Add(D(194, "Корзины на мосту", 5, M(Portal3, 3, "...1..1..", ".........", ".........", ".........", ".u..u....")).Goal("basket").Goal("crate")
                .Basket("corn", 3).Basket("apple", 3).With(d => d.Portals = P3()));
            L.Add(D(195, "Сорная грядка", 6, M(Full, 3, ".b.w.w.b.", ".........", ".........", "....b....")).Goal("crop", 4).Goal("weed"));
            L.Add(D(196, "Лёд на островах", 5, M(Islands, 2, "Iii...iiI", ".........", ".........", ".........", "...iii...")).Goal("ice").Collect("corn", 10));
            L.Add(D(197, "Ящики и цепи", 6, M(Cross, 3, "l2l2l2l2l", ".........", "2l2l2l2l2")).Goal("crate").Goal("chain"));
            L.Add(D(198, "Цыплята-путешественники", 5, M(Portal2, 0, "k...k....", ".........", ".........", ".........", ".........", ".........", ".........", "....1.1.1"))
                .Goal("chick", 4).Goal("crate").Chicks(2, 3, 2).With(d => { d.Portals = P2(); d.ChickExits = Cells((4, 8), (5, 8), (6, 8), (7, 8), (8, 8)); }));
            L.Add(D(199, "Канун праздника", 6, M(Ring, 2, "GGhGGGhGG", ".........", ".........", ".........", "GGhGGGhGG")).Goal("honey").Goal("grass").Collect("tomato", 10));
            L.Add(D(200, "Большая ярмарка", 6, M(Full, 2, "GGGGGGGGG", "G2b2m2b2G", "GGGGGGGGG")).Goal("grass").Goal("crate").Goal("crop", 4).Goal("honey"));
        }
    }
}

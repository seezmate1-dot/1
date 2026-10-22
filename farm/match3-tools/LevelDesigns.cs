using System.Collections.Generic;
using SunnyValley.Core.Match3;

namespace SunnyValley.Tools
{
    /// <summary>
    /// The first 60 levels: shapes, obstacles and goals by hand; the move counts are found by the tuner
    /// (Program "build") so that the average bot wins at the target rate. Mechanics come one at a time:
    /// match → grass → pitchfork → bee → crates → bomb → rainbow → ice → chicks → chains → beds (1–20),
    /// honey → baskets → stones → walls → weed (21–25), portals (32), 6 crops (36).
    /// Waves after 20: every 10th level is hard (35–50%), the one after it is easy.
    /// </summary>
    public static class LevelDesigns
    {
        static double TargetFor(int id)
        {
            if (id <= 20) return 0.93;
            if (id % 10 == 0) return 0.42;
            int k = id % 10; // 1..9 within the wave
            double start = id < 30 ? 0.86 : id < 40 ? 0.84 : id < 50 ? 0.82 : 0.80;
            double end = id < 30 ? 0.64 : id < 40 ? 0.62 : id < 50 ? 0.60 : 0.57;
            if (k == 1 && id > 21) return 0.90; // easy level right after a hard one
            return start + (end - start) * (k - 1) / 8.0;
        }

        static Design D(int id, string name, int colors, params string[] map)
        {
            return new Design { Id = id, Name = name, Colors = colors, Map = map, Target = TargetFor(id), Hard = id > 20 && id % 10 == 0 };
        }

        static SpecialData Sp(int x, int y, string kind, string color = null) => new SpecialData { X = x, Y = y, Kind = kind, Color = color };
        static PortalData Portal(int fx, int fy, int tx, int ty) => new PortalData { From = new[] { fx, fy }, To = new[] { tx, ty } };
        static WallData Wall(int x, int y, string side) => new WallData { X = x, Y = y, Side = side };

        public static List<Design> All()
        {
            var L = new List<Design>();

            // ---------------------------------------------------------------- 1–10
            L.Add(D(1, "Первые всходы", 4,
                "#######", "#######", "#######", "#######", "#######", "#######", "#######").Collect("tomato", 12));
            L.Add(D(2, "Два урожая", 4,
                " ###### ", "########", "########", "########", "########", "########", "########", " ###### ").Collect("corn", 15).Collect("apple", 15));
            var d3 = D(3, "Заросший двор", 4,
                "########", "########", "########", "##gggg##", "##gggg##", "########", "########", "########").Goal("grass");
            d3.Intro = "grass"; L.Add(d3);
            var d4 = D(4, "Вилы", 4,
                "########", "########", "########", "########", "########", "########", "########", "########").Collect("carrot", 22);
            d4.Intro = "lineH"; d4.Specials = new List<SpecialData> { Sp(3, 4, "lineH", "tomato") }; L.Add(d4);
            L.Add(D(5, "Грядки в траве", 4,
                " ####### ", "#########", "#gggggg##", "#########", "#########", "##gggggg#", "#########", " ####### ").Goal("grass").Collect("tomato", 12));
            var d6 = D(6, "Пчела", 4,
                "########", "########", "###gg###", "##gggg##", "##gggg##", "###gg###", "########", "########").Goal("grass").Collect("apple", 15);
            d6.Intro = "bee"; d6.Specials = new List<SpecialData> { Sp(1, 6, "bee", "corn") }; L.Add(d6);
            var d7 = D(7, "Ящики", 5,
                "#########", "#########", "#########", "#########", "#1111111#", "#########", "#########", "#########", "#########").Goal("crate");
            d7.Intro = "crate"; L.Add(d7);
            var d8 = D(8, "Бочка", 5,
                "#########", "#########", "#########", "##11111##", "#########", "#########", "#########", "#########", "#########").Goal("crate").Collect("corn", 15);
            d8.Intro = "bomb"; d8.Specials = new List<SpecialData> { Sp(4, 6, "bomb", "apple") }; L.Add(d8);
            L.Add(D(9, "Тяжёлые ящики", 5,
                "#########", "#########", "#########", "#########", "#########", "#########", "22#####22", "22#####22", "#########").Goal("crate").Collect("tomato", 15));
            var d10 = D(10, "Радуга-подсолнух", 5,
                "#########", "#########", "#########", "#########", "#########", "ggggggggg", "ggggggggg", "ggggggggg", "ggggggggg").Goal("grass");
            d10.Intro = "rainbow"; d10.Specials = new List<SpecialData> { Sp(4, 2, "rainbow") }; L.Add(d10);

            // ---------------------------------------------------------------- 11–20
            var d11 = D(11, "Иней", 5,
                "#########", "#########", "####i####", "###iii###", "##iiiii##", "###iii###", "####i####", "#########", "#########").Goal("ice");
            d11.Intro = "ice"; L.Add(d11);
            L.Add(D(12, "Сердце сада", 5,
                " ##   ## ", "#### ####", "#########", "###GGG###", "###GGG###", " ####### ", "  #####  ", "   ###   ", "    #    ").Goal("grass").Collect("corn", 12));
            var d13 = D(13, "Цыплята", 5,
                "#k#####k#", "#########", "#########", "#########", "#########", "#########", "#########", "#########", "#########").Goal("chick", 3);
            d13.Intro = "chick"; d13.Chicks = new ChickRules { Spawn = 1, MaxOnBoard = 2, Gap = 3 }; L.Add(d13);
            L.Add(D(14, "Ярмарка", 5,
                "#########", "#########", "#########", "#########", "#########", "#########", "#########", "#########", "#########").Collect("tomato", 18).Collect("carrot", 18).Collect("eggplant", 18));
            var d15 = D(15, "Цепи", 5,
                "#########", "#########", "#########", "#########", "#lllllll#", "#########", "#########", "#########", "#########").Goal("chain").Collect("apple", 15);
            d15.Intro = "chain"; L.Add(d15);
            L.Add(D(16, "Амбарные завалы", 5,
                "#########", "#########", "#########", "#1111111#", "#1#333#1#", "#1#333#1#", "#1111111#", "#########", "#########").Goal("crate"));
            var d17 = D(17, "Огород", 5,
                "#########", "#########", "#########", "##b###b##", "#########", "#########", "#########", "#########", "#########").Goal("crop", 4);
            d17.Intro = "bed"; L.Add(d17);
            L.Add(D(18, "Крепкий лёд", 5,
                "#########", "#########", "#########", "#########", "#IIIIIII#", "#iiiiiii#", "#########", "#########", "#########").Goal("ice").Collect("corn", 12));
            var d19 = D(19, "Цыплята и ящики", 5,
                "#k##k##k#", "#########", "#########", "#########", "#########", "1#1#1#1#1", "#########", "#########", "#########").Goal("chick", 3).Goal("crate");
            L.Add(d19);
            L.Add(D(20, "Двор прибран", 5,
                "#########", "#########", "#########", "ggg###ggg", "ggg1#1ggg", "ggg1#1ggg", "ggg###ggg", "#########", "#########").Goal("grass").Goal("crate"));

            // ---------------------------------------------------------------- 21–30
            var d21 = D(21, "Мёд", 5,
                "#########", "#########", "#########", "####h####", "###hhh###", "####h####", "#########", "#########", "#########").Goal("honey").Collect("carrot", 15);
            d21.Intro = "honey"; L.Add(d21);
            var d22 = D(22, "Корзины", 5,
                "#########", "#########", "#########", "#########", "##u###u##", "#########", "#########", "#########", "#########").Goal("basket");
            d22.Intro = "basket"; d22.Baskets.Add(("tomato", 6)); d22.Baskets.Add(("corn", 6)); L.Add(d22);
            var d23 = D(23, "Камни", 5,
                "#########", "#########", "#########", "#s##S##s#", "#########", "##s###s##", "#########", "#########", "#########").Goal("stone").Collect("apple", 15);
            d23.Intro = "stone"; L.Add(d23);
            var d24 = D(24, "Изгородь", 5,
                "#########", "#########", "#########", "#########", "GGGGGGGGG", "GGGGGGGGG", "ggggggggg", "#########", "#########").Goal("grass").Collect("corn", 12);
            d24.Intro = "wall";
            d24.Walls = new List<WallData>();
            for (int y = 0; y < 9; y++) d24.Walls.Add(Wall(3, y, "right"));
            d24.Walls.Add(Wall(6, 2, "down")); d24.Walls.Add(Wall(7, 2, "down"));
            L.Add(d24);
            var d25 = D(25, "Сорняк", 5,
                "#########", "#########", "#########", "#########", "#w##w##w#", "#########", "#########", "#########", "#########").Goal("weed").Collect("carrot", 15);
            d25.Intro = "weed"; L.Add(d25);
            L.Add(D(26, "Скованный лёд", 5,
                "#########", "#########", "#########", "#IIIIIII#", "#LLLLLLL#", "#iiiiiii#", "#########", "#########", "#########").Goal("ice").Goal("chain"));
            L.Add(D(27, "Густая трава", 5,
                "#########", "#########", "GGGGGGGGG", "GGGGGGGGG", "GGGGGGGGG", "ggggggggg", "ggggggggg", "#########", "#########").Goal("grass").Collect("eggplant", 10));
            var d28 = D(28, "Через овраг", 5,
                "#k#####k#", "#########", "#########", "### # ###", "### # ###", "#########", "#########", "#########", "#########").Goal("chick", 4);
            d28.Chicks = new ChickRules { Spawn = 2, MaxOnBoard = 2, Gap = 2 }; L.Add(d28);
            L.Add(D(29, "Медовые грядки", 5,
                "#########", "#########", "#########", "#b##h##b#", "###hhh###", "#########", "#########", "#########", "#########").Goal("crop", 4).Goal("honey"));
            L.Add(D(30, "Большая уборка", 5,
                "#########", "#########", "GGGGGGGGG", "GG11111GG", "GG1###1GG", "GG11111GG", "GGGGGGGGG", "#########", "#########").Goal("grass").Goal("crate"));

            // ---------------------------------------------------------------- 31–40
            L.Add(D(31, "Отдых на лугу", 4,
                " ####### ", "#########", "#########", "#########", "#########", "#########", "#########", "#########", " ####### ").Collect("apple", 20).Collect("tomato", 20));
            var d32 = D(32, "Порталы", 5,
                "#####    ", "#####    ", "#####    ", "#gggg    ", "         ", "    ggggg", "    #####", "    #####", "    #####").Goal("grass");
            d32.Intro = "portal";
            d32.Portals = new List<PortalData>();
            for (int x = 0; x < 5; x++) d32.Portals.Add(Portal(x, 3, x + 4, 5));
            L.Add(d32);
            L.Add(D(33, "Камни в ящиках", 5,
                "#########", "#########", "#########", "#3S3#3S3#", "#2#2#2#2#", "#########", "#########", "#########", "#########").Goal("stone").Goal("crate"));
            var d34 = D(34, "Полные корзины", 5,
                "#########", "#########", "#########", "#u#####u#", "#########", "#########", "####u####", "#########", "#########").Goal("basket").Collect("carrot", 15);
            d34.Baskets.Add(("tomato", 5)); d34.Baskets.Add(("apple", 5)); d34.Baskets.Add(("corn", 6)); L.Add(d34);
            L.Add(D(35, "Сорная трава", 5,
                "#########", "#########", "#########", "GGGGGGGGG", "GwGGwGGwG", "GGGGGGGGG", "#########", "#########", "#########").Goal("grass").Goal("weed"));
            var d36 = D(36, "Шесть культур", 6,
                "#########", "#########", "#########", "#########", "##11111##", "#########", "#########", "#########", "#########").Collect("blueberry", 12).Goal("crate");
            d36.Intro = "colors6"; L.Add(d36);
            var d37 = D(37, "Цыплята за порталом", 5,
                "#k###    ", "#####    ", "#####    ", "#####    ", "         ", "    #####", "    #####", "    #####", "    #####").Goal("chick", 3);
            d37.Portals = new List<PortalData>();
            for (int x = 0; x < 5; x++) d37.Portals.Add(Portal(x, 3, x + 4, 5));
            d37.Chicks = new ChickRules { Spawn = 2, MaxOnBoard = 2, Gap = 2 }; L.Add(d37);
            L.Add(D(38, "Ледяные цепи", 6,
                "#########", "#########", "#########", "#IlIlIlI#", "#lIlIlIl#", "#########", "#########", "#########", "#########").Goal("ice").Goal("chain"));
            L.Add(D(39, "Каменный огород", 5,
                "#########", "#########", "#########", "#b#s#s#b#", "#########", "#########", "#########", "#########", "#########").Goal("crop", 5).Goal("stone"));
            L.Add(D(40, "Запущенный сад", 5,
                "#########", "#########", "GGGGGGGGG", "GnnnnnnnG", "G2#2#2#2G", "GGGGGGGGG", "#########", "#########", "#########").Goal("grass").Goal("chain").Goal("crate"));

            // ---------------------------------------------------------------- 41–50
            L.Add(D(41, "Тёплый вечер", 4,
                "#########", "#########", "#########", "###ggg###", "###ggg###", "###ggg###", "#########", "#########", "#########").Goal("grass").Collect("corn", 15));
            L.Add(D(42, "Мёд на льду", 6,
                "#########", "#########", "#########", "#IIIhIII#", "#iihiiii#", "#########", "#########", "#########", "#########").Goal("ice").Goal("honey"));
            var d43 = D(43, "Цыплята в цепях", 5,
                "#k#k#k#k#", "#########", "#########", "#########", "#lllllll#", "#########", "#########", "#########", "#########").Goal("chick", 4).Goal("chain");
            L.Add(d43);
            var d44 = D(44, "Корзины в ящиках", 5,
                "#########", "#########", "#########", "#1u1#1u1#", "#11111111".Substring(0, 9), "#########", "#########", "#########", "#########").Goal("basket").Goal("crate");
            d44.Baskets.Add(("eggplant", 6)); d44.Baskets.Add(("carrot", 6)); L.Add(d44);
            var d45 = D(45, "Загоны", 6,
                "#########", "#########", "#########", "GGGGGGGGG", "GGGGGGGGG", "#########", "#########", "#########", "#########").Goal("grass");
            d45.Walls = new List<WallData>();
            for (int y = 0; y < 9; y++) { d45.Walls.Add(Wall(2, y, "right")); d45.Walls.Add(Wall(5, y, "right")); }
            L.Add(d45);
            L.Add(D(46, "Сорняк у грядок", 6,
                "#########", "#########", "#########", "#b#w#w#b#", "#########", "#########", "#########", "#########", "#########").Goal("crop", 4).Goal("weed"));
            L.Add(D(47, "Камни на траве", 6,
                "#########", "#########", "GGGGGGGGG", "gQgSgSgQg", "GGGGGGGGG", "#########", "#########", "#########", "#########").Goal("grass").Goal("stone"));
            var d48 = D(48, "Ящики за порталом", 5,
                "#####    ", "#####    ", "#####    ", "#####    ", "         ", "    #####", "    #####", "    2#2#2", "    #2#2#").Goal("crate").Collect("tomato", 15);
            d48.Portals = new List<PortalData>();
            for (int x = 0; x < 5; x++) d48.Portals.Add(Portal(x, 3, x + 4, 5));
            L.Add(d48);
            var d49 = D(49, "Липкие цыплята", 5,
                "#k##k##k#", "#########", "#########", "#########", "##hhhhh##", "#########", "#########", "#########", "#########").Goal("chick", 4).Goal("honey");
            d49.Chicks = new ChickRules { Spawn = 1, MaxOnBoard = 3, Gap = 3 }; L.Add(d49);
            L.Add(D(50, "Ледяная скала", 6,
                "#########", "#########", "#########", "#IIsIsII#", "#IIIIIII#", "#########", "#########", "#########", "#########").Goal("ice").Goal("stone").Collect("blueberry", 10));

            // ---------------------------------------------------------------- 51–60
            L.Add(D(51, "Солнечный полдень", 4,
                "#########", "#########", "#########", "#########", "##11111##", "#########", "#########", "#########", "#########").Goal("crate").Collect("tomato", 15));
            L.Add(D(52, "Пёстрый луг", 6,
                "#########", "#########", "#########", "#GGGGGGG#", "#GgggggG#", "#GGGGGGG#", "#########", "#########", "#########").Goal("grass"));
            var d53 = D(53, "Склад корзин", 5,
                "#########", "#########", "#########", "#3u###u3#", "#33###33#", "#########", "#########", "#########", "#########").Goal("basket").Goal("crate");
            d53.Baskets.Add(("apple", 7)); d53.Baskets.Add(("corn", 7)); L.Add(d53);
            L.Add(D(54, "Двойные цепи", 5,
                "#########", "#########", "#########", "#LLLLLLL#", "#ggggggg#", "#ggggggg#", "#########", "#########", "#########").Goal("chain").Goal("grass"));
            var d55 = D(55, "Цыплята в сорняке", 5,
                "#k##k##k#", "#########", "#########", "#########", "#w#####w#", "#########", "#########", "#########", "#########").Goal("chick", 5).Goal("weed");
            d55.Chicks = new ChickRules { Spawn = 2, MaxOnBoard = 3, Gap = 2 }; L.Add(d55);
            L.Add(D(56, "Огород за забором", 5,
                "#########", "#########", "#########", "#b2#b#2b#", "#2#####2#", "#########", "#########", "#########", "#########").Goal("crop", 6).Goal("crate"));
            var d57 = D(57, "Медовый портал", 5,
                "#####    ", "#h###    ", "###h#    ", "#####    ", "         ", "    #####", "    #####", "    #####", "    #####").Goal("honey").Collect("carrot", 15);
            d57.Portals = new List<PortalData>();
            for (int x = 0; x < 5; x++) d57.Portals.Add(Portal(x, 3, x + 4, 5));
            L.Add(d57);
            L.Add(D(58, "Морозный камень", 6,
                "#########", "#########", "#########", "#jjSjSjj#", "#GGGGGGG#", "#########", "#########", "#########", "#########").Goal("ice").Goal("stone").Goal("grass"));
            var d59 = D(59, "Всё и сразу", 6,
                "#k#####k#", "#########", "#########", "#gg1#1gg#", "#gg1#1gg#", "#########", "#########", "#########", "#########").Goal("chick", 3).Goal("grass").Goal("crate");
            d59.Chicks = new ChickRules { Spawn = 1, MaxOnBoard = 2, Gap = 3 }; L.Add(d59);
            L.Add(D(60, "Праздник урожая", 6,
                "#########", "#########", "GGGGGGGGG", "G2nb2bn2G", "GGGGGGGGG", "#########", "#########", "#########", "#########").Goal("grass").Goal("crate").Goal("crop", 4));

            return L;
        }
    }
}

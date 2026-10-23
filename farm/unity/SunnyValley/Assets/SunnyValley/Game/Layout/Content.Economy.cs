using System.Collections.Generic;

namespace SunnyValley.Layout
{
    // What the windows of the farm economy (chapter 2, GDD 6) show: fields and the orchard, animals, workshops with
    // their queues, the barn, the order board and the truck, the town. Every one of them is the same kind of window
    // (Popups.Economy): an optional header well, an optional production queue, a scrolling list of rows or a grid of
    // cards, up to two buttons at the bottom. Game/Economy fills these from MetaGame; the tests feed long texts.

    /// <summary>The colour of a button: what pressing it does (green: do it, blue: crystals, grey: not possible now).</summary>
    public enum Tone
    {
        Green,
        Yellow,
        Blue,
        Red,
        Grey,
    }

    /// <summary>A button of a row, a header or the bottom of a window: a text and an optional icon before it.</summary>
    public sealed class RowButton
    {
        public string Text;
        public string Icon;
        public Tone Tone;

        public RowButton(string text, string icon = null, Tone tone = Tone.Green)
        {
            Text = text;
            Icon = icon;
            Tone = tone;
        }
    }

    /// <summary>An icon with a short amount: «1/2» of a recipe's input, «×3» of an order. Missing ones read red.</summary>
    public sealed class Chip
    {
        public string Icon;
        public string Text;
        public bool Missing;

        public Chip(string icon, string text, bool missing = false)
        {
            Icon = icon;
            Text = text;
            Missing = missing;
        }
    }

    public enum RowKind
    {
        /// <summary>A picture, a title, a line under it (text, chips or a timer bar) and buttons on the right.</summary>
        Row,
        /// <summary>A caption between groups of rows («Грядки», «Сад»).</summary>
        Section,
    }

    public sealed class EconomyRow
    {
        public RowKind Kind = RowKind.Row;
        public string Icon;
        public string Title;
        /// <summary>The line under the title (ignored when there are chips or a bar).</summary>
        public string Sub;
        /// <summary>Inputs of a recipe, goods of an order or a crate, tools of an upgrade (up to three).</summary>
        public List<Chip> Chips = new List<Chip>();
        /// <summary>0..1: a timer bar under the title with ProgressText on it; negative: none.</summary>
        public float Progress = -1;
        public string ProgressText;
        /// <summary>The main button on the right (null: none).</summary>
        public RowButton Button;
        /// <summary>A round icon-only button left of the main one: discard, plant from the barn (null: none).</summary>
        public RowButton Alt;
        /// <summary>Not open yet: the row is greyed.</summary>
        public bool Dim;
        /// <summary>Cards: the one picked (the bottom buttons act on it).</summary>
        public bool Picked;
        /// <summary>Cards: who asks (the order's customer), on top of the card.</summary>
        public string Caption;
        /// <summary>Cards: what it pays, at the bottom.</summary>
        public List<Chip> Rewards = new List<Chip>();

        public static EconomyRow Section(string title) => new EconomyRow { Kind = RowKind.Section, Title = title };
    }

    /// <summary>A slot of a workshop's queue: done (tap to collect), running (a timer), waiting, free, or one to buy.</summary>
    public enum SlotState
    {
        Free,
        Ready,
        Running,
        Waiting,
        Buy,
    }

    public sealed class QueueSlot
    {
        public SlotState State;
        public string Icon;
        /// <summary>A timer, «Готово», the price of a slot to buy.</summary>
        public string Text;

        public QueueSlot(SlotState state, string icon = null, string text = null)
        {
            State = state;
            Icon = icon;
            Text = text;
        }
    }

    /// <summary>A window of the farm economy.</summary>
    public sealed class EconomyInfo
    {
        /// <summary>The layout's name and the prefix of every id in it: «fields», «barn», «orders»…</summary>
        public string Id = "economy";
        public string Title = "";

        /// <summary>The header well (null text: no header): a line, then a bar or chips, a button on the right.</summary>
        public string HeaderText;
        public float HeaderBar = -1;
        public string HeaderBarText;
        public List<Chip> HeaderChips = new List<Chip>();
        public RowButton HeaderButton;

        /// <summary>A workshop's production queue over the recipes (null: none).</summary>
        public List<QueueSlot> Queue;

        /// <summary>Rows laid out as a grid of cards, three a row (the order board), instead of a list.</summary>
        public bool Cards;
        public List<EconomyRow> Rows = new List<EconomyRow>();
        /// <summary>Shown instead of the list when there are no rows.</summary>
        public string Empty = "Пока пусто";

        /// <summary>Up to two buttons at the bottom of the window.</summary>
        public List<RowButton> Footer = new List<RowButton>();
    }

    /// <summary>A question before something costs crystals or needs a detour: a picture, a line, a button.</summary>
    public sealed class ConfirmInfo
    {
        public string Title = "Ускорить?";
        public string Icon = Icons.Crystal;
        public string Text = "";
        public RowButton Button = new RowButton("Да", null, Tone.Green);
    }
}

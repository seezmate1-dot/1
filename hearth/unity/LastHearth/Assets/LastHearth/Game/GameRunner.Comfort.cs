using LastHearth.Core;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// Conveniences of the settings (GDD 14, MODES.md 4.4): «Экономия батареи» runs the game at 30
    /// frames a second instead of 60 (remembered on the phone).
    /// </summary>
    public sealed partial class GameRunner
    {
        const string BatteryKey = "lasthearth.batterySaver";

        bool? batterySaver;

        bool BatterySaver => batterySaver ?? (batterySaver = PlayerPrefs.GetInt(BatteryKey, 0) == 1).Value;

        void ApplyFrameRate() => Application.targetFrameRate = BatterySaver ? 30 : 60;

        void SetBatterySaver(bool on)
        {
            batterySaver = on;
            PlayerPrefs.SetInt(BatteryKey, on ? 1 : 0);
            PlayerPrefs.Save();
            ApplyFrameRate();
        }

        void DrawComfortSettings(long now)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(BatterySaver ? "Экономия батареи: вкл (30 кадров)" : "Экономия батареи: выкл (60 кадров)"))
            {
                SetBatterySaver(!BatterySaver);
                message = BatterySaver ? "Экономия батареи: 30 кадров в секунду" : "Плавно: 60 кадров в секунду";
            }
            GUILayout.EndHorizontal();
        }
    }
}

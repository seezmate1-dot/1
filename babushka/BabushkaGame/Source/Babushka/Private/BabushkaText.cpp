// Copyright (c) Babushka project. All rights reserved.

#include "BabushkaText.h"

#include "BabushkaTypes.h"

#include <initializer_list>

#define LOCTEXT_NAMESPACE "Babushka"

namespace
{
	FText Pick(std::initializer_list<FText> Options)
	{
		const int32 Index = FMath::RandRange(0, static_cast<int32>(Options.size()) - 1);
		return *(Options.begin() + Index);
	}
}

FText BabushkaText::GrandmaLine(EGrandmaLine Line)
{
	switch (Line)
	{
	case EGrandmaLine::Idle:
		return Pick({ LOCTEXT("Idle1", "Пирожки стынут…"), LOCTEXT("Idle2", "Где мои очки…"), LOCTEXT("Idle3", "Сериал скоро…"),
			LOCTEXT("Idle4", "Тесто подходит…"), LOCTEXT("Idle5", "Шапку бы им…") });
	case EGrandmaLine::Suspicious:
		return Pick({ LOCTEXT("Sus1", "Кто там шуршит?"), LOCTEXT("Sus2", "Внучек, это ты?"), LOCTEXT("Sus3", "Ой, кто там?") });
	case EGrandmaLine::Chase:
		return Pick({ LOCTEXT("Chase1", "А ну стой! Ты не доел!"), LOCTEXT("Chase2", "Ещё пирожочек!"), LOCTEXT("Chase3", "Ты чего такой худой?") });
	case EGrandmaLine::ChaseBall: return LOCTEXT("ChaseBall", "Куда покатился?!");
	case EGrandmaLine::Feeding:
		return Pick({ LOCTEXT("Feed1", "Ты чего такой худой?"), LOCTEXT("Feed2", "Ешь, ешь, растёшь же!"), LOCTEXT("Feed3", "С капустой! Горяченькие!") });
	case EGrandmaLine::FeedingBall: return LOCTEXT("FeedBall", "Ну вот, хоть поправился!");
	case EGrandmaLine::AfterFeeding: return LOCTEXT("AfterFeed", "А теперь спать!");
	case EGrandmaLine::LostTarget: return LOCTEXT("Lost", "Куда делся?..");
	case EGrandmaLine::FalseAlarm: return LOCTEXT("False", "Показалось…");
	case EGrandmaLine::WokeUp: return LOCTEXT("Woke", "Хр… А? Кто тут?");
	case EGrandmaLine::Sleep: return LOCTEXT("Sleep", "Хр-р-р…");
	case EGrandmaLine::GoingToSleep: return LOCTEXT("ToSleep", "Прилягу на минутку…");
	case EGrandmaLine::TV: return LOCTEXT("TV", "Ой, мой сериал!");
	case EGrandmaLine::TVOver: return LOCTEXT("TVOver", "Ну и концовка…");
	case EGrandmaLine::SawHiding: return LOCTEXT("SawHide", "Я всё видела!");
	case EGrandmaLine::Stunned: return LOCTEXT("Stunned", "Ох! Колобок проклятый!");
	case EGrandmaLine::Recovered: return LOCTEXT("Recovered", "Ох, поясница…");
	}
	return FText::GetEmpty();
}

FText BabushkaText::FullnessName(int32 Level)
{
	switch (FMath::Clamp(Level, 0, BabushkaFullness::MaxLevel))
	{
	case 0: return LOCTEXT("Full0", "Как спичка");
	case 1: return LOCTEXT("Full1", "Нормальный");
	case 2: return LOCTEXT("Full2", "Упитанный");
	case 3: return LOCTEXT("Full3", "Щёчки");
	case 4: return LOCTEXT("Full4", "Пирожок");
	default: return LOCTEXT("Full5", "Колобок");
	}
}

FText BabushkaText::InteractLabel(uint8 Kind)
{
	switch (static_cast<EBabushkaInteractKind>(Kind))
	{
	case EBabushkaInteractKind::Hide: return LOCTEXT("LHide", "спрятаться");
	case EBabushkaInteractKind::Shoes: return LOCTEXT("LShoes", "кроссовки");
	case EBabushkaInteractKind::Phone: return LOCTEXT("LPhone", "телефон из серванта");
	case EBabushkaInteractKind::Money: return LOCTEXT("LMoney", "деньги из банки");
	case EBabushkaInteractKind::Key: return LOCTEXT("LKey", "ключ от калитки");
	case EBabushkaInteractKind::Pie: return LOCTEXT("LPie", "съесть пирожок");
	case EBabushkaInteractKind::TV: return LOCTEXT("LTV", "включить сериал");
	case EBabushkaInteractKind::Gate: return LOCTEXT("LGate", "открыть калитку");
	}
	return FText::GetEmpty();
}

FText BabushkaText::Toast(FName Id)
{
	static const TMap<FName, FText> Table = {
		{ FName(TEXT("Start")), LOCTEXT("TStart", "Найдите кроссовки в сенях и выбирайтесь за забор") },
		{ FName(TEXT("Hidden")), LOCTEXT("THidden", "Спрятались. E — вылезти") },
		{ FName(TEXT("HiddenSeen")), LOCTEXT("THiddenSeen", "Бабушка видела, куда вы спрятались!") },
		{ FName(TEXT("Unhide")), LOCTEXT("TUnhide", "Вылезли") },
		{ FName(TEXT("WardrobeFull")), LOCTEXT("TWardrobe", "В шкаф уже не помещаетесь") },
		{ FName(TEXT("BallCantHide")), LOCTEXT("TBallHide", "Колобку не спрятаться") },
		{ FName(TEXT("Phone")), LOCTEXT("TPhone", "Телефон у вас. Хрусталь звякнул…") },
		{ FName(TEXT("Money")), LOCTEXT("TMoney", "Деньги на дискотеку есть") },
		{ FName(TEXT("Shoes")), LOCTEXT("TShoes", "Кроссовки! Теперь можно уходить") },
		{ FName(TEXT("Key")), LOCTEXT("TKey", "Ключ от калитки. Куры всё слышали!") },
		{ FName(TEXT("PieRush")), LOCTEXT("TPie", "Сахарный рывок 8 секунд! Сытость +1") },
		{ FName(TEXT("PieEmpty")), LOCTEXT("TPieEmpty", "Пирожки кончились") },
		{ FName(TEXT("PieBall")), LOCTEXT("TPieBall", "В колобка больше не лезет") },
		{ FName(TEXT("TVOn")), LOCTEXT("TTVOn", "Включили бабушкин сериал") },
		{ FName(TEXT("TVCooldown")), LOCTEXT("TTVCd", "Сериал ещё не начался") },
		{ FName(TEXT("GateLocked")), LOCTEXT("TGateLocked", "Калитка заперта. Ключ где-то в курятнике") },
		{ FName(TEXT("GateOpen")), LOCTEXT("TGateOpen", "Калитка скрипнула и открылась") },
		{ FName(TEXT("NoShoes")), LOCTEXT("TNoShoes", "В тапках на дискотеку? Сначала кроссовки из сеней") },
		{ FName(TEXT("TooFatForGap")), LOCTEXT("TGap", "Под забором пролезают только худые (сытость 0–1)") },
		{ FName(TEXT("FenceBroken")), LOCTEXT("TFence", "Забор снесён! Катитесь наружу") },
		{ FName(TEXT("FenceTooSlow")), LOCTEXT("TFenceSlow", "Разгонитесь сильнее, чтобы пробить забор") },
		{ FName(TEXT("Banya")), LOCTEXT("TBanya", "Пропотели в бане: сытость −1") },
		{ FName(TEXT("Chickens")), LOCTEXT("TChickens", "Куры раскудахтались!") },
		{ FName(TEXT("BallHitGrandma")), LOCTEXT("TBallHit", "Колобок сбил бабушку!") },
		{ FName(TEXT("BecameBall")), LOCTEXT("TBall", "Вы колобок! Разгоняйтесь и сносите забор") },
		{ FName(TEXT("FullnessUp")), LOCTEXT("TFullUp", "Сытость +1") },
		{ FName(TEXT("Restart")), LOCTEXT("TRestart", "R — ещё ночь") },
		{ FName(TEXT("HudShoes")), LOCTEXT("HShoes", "Кроссовки") },
		{ FName(TEXT("HudPhone")), LOCTEXT("HPhone", "Телефон") },
		{ FName(TEXT("HudMoney")), LOCTEXT("HMoney", "Деньги") },
		{ FName(TEXT("HudKey")), LOCTEXT("HKey", "Ключ от калитки") },
		{ FName(TEXT("HudFullness")), LOCTEXT("HFull", "Сытость") },
		{ FName(TEXT("StatTime")), LOCTEXT("STime", "Время") },
		{ FName(TEXT("StatScore")), LOCTEXT("SScore", "Очки") },
		{ FName(TEXT("StatPies")), LOCTEXT("SPies", "Съедено пирожков") },
		{ FName(TEXT("StatCaught")), LOCTEXT("SCaught", "Поймала бабушка") }
	};
	const FText* Found = Table.Find(Id);
	return Found ? *Found : FText::FromName(Id);
}

FText BabushkaText::EndTitle(bool bEscaped, int32 Level, int32 Pies)
{
	if (!bEscaped) return LOCTEXT("EndStay", "Остался на завтрак");
	if (Level >= BabushkaFullness::MaxLevel) return LOCTEXT("EndBall", "Колобок: от бабушки ушёл");
	if (Level == 0) return LOCTEXT("EndThin", "Худой как спичка");
	if (Pies >= 4) return LOCTEXT("EndPies", "Пирожковый магнат");
	return LOCTEXT("EndOk", "Сбежал на дискотеку");
}

FText BabushkaText::EndLine(bool bEscaped, int32 Level, int32 Pies)
{
	if (!bEscaped) return LOCTEXT("LineStay", "«Вставай, блины стынут!»");
	if (Level >= BabushkaFullness::MaxLevel) return LOCTEXT("LineBall", "«Хоть поправился немножко…»");
	if (Level == 0) return LOCTEXT("LineThin", "«Кожа да кости! Кто ж тебя такого полюбит?»");
	if (Pies >= 4) return LOCTEXT("LinePies", "«Вот это аппетит, весь в деда!»");
	return LOCTEXT("LineOk", "«Хоть бы шапку надел…»");
}

#undef LOCTEXT_NAMESPACE

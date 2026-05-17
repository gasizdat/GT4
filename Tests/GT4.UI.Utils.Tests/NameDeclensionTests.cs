using Xunit;

namespace GT4.UI.Utils.Tests;

public class NameDeclensionTests
{
  [Theory]
  // Rule 1
  [InlineData("Александр", "Александрович", "Александровна")]
  [InlineData("иван", "иванович", "ивановна")]
  [InlineData("Газмат", "Газматович", "Газматовна")]
  // Rule 2
  [InlineData("Жорж", "Жоржевич", "Жоржевна")]
  [InlineData("Януш", "Янушевич", "Янушевна")]
  [InlineData("Милич", "Миличевич", "Миличевна")]
  [InlineData("Франц", "Францевич", "Францевна")]
  // Rule 3.1
  [InlineData("Антипа", "Антипович","Антиповна")]
  [InlineData("Вавила", "Вавилович","Вавиловна")]
  // Rule 3.2
  [InlineData("Никита", "Никитич", "Никитична")]
  [InlineData("Мина", "Минич", "Минична")]
  [InlineData("Савва", "Саввич", "Саввична")]
  // Rule 4
  [InlineData("Василько", "Василькович", "Васильковна")]
  [InlineData("Михайло", "Михайлович", "Михайловна")]
  [InlineData("Отто", "Оттович", "Оттовна")]
  // Rule 5
  [InlineData("Важа", "Важевич", "Важевна")]
  [InlineData("Гоча", "Гочевич", "Гочевна")]
  // Rule 6
  [InlineData("Игорь", "Игоревич", "Игоревна")]
  [InlineData("Цезарь", "Цезаревич", "Цезаревна")]
  [InlineData("Виль", "Вилевич", "Вилевна")]
  [InlineData("Камиль", "Камилевич", "Камилевна")]
  // Rule 7
  [InlineData("Аарне", "Аарневич", "Аарневна")]
  [InlineData("Григоре", "Григоревич", "Григоревна")]
  [InlineData("Вилье", "Вильевич", "Вильевна")]
  // Rule 8
  [InlineData("Вилли", "Виллиевич", "Виллиевна")]
  [InlineData("Илмари", "Илмариевич", "Илмариевна")]
  // Rule 9.1
  [InlineData("Василий", "Васильевич", "Васильевна")]
  [InlineData("Марий", "Марьевич", "Марьевна")]
  [InlineData("Юлий", "Юльевич", "Юльевна")]
  [InlineData("Иннокентий", "Иннокентьевич", "Иннокентьевна")]
  // Rule 9.2
  [InlineData("Никий", "Никиевич", "Никиевна")]
  [InlineData("Люций", "Люциевич", "Люциевна")]
  [InlineData("Стахий", "Стахиевич", "Стахиевна")]
  // Rule 10
  [InlineData("Менея", "Менеевич", "Менеевна")]
  [InlineData("Захария", "Захариевич", "Захариевна")]

  // Rule 11
  /* The rule 11 doesn't work as we don't know the stress in the worlds
  [InlineData("Айбу", "Айбуевич", "Айбуевна")]
  [InlineData("Бадма", "Бадмаевич", "Бадмаевна")]
  [InlineData("Бату", "Бутуевич", "Батуевна")]
  [InlineData("Вали", "Валиевич", "Валиевна")]
  [InlineData("Дакко", "Даккоевич", "Даккоевна")]
  [InlineData("Исе", "Исеевич", "Исеевна")]*/

  // Rule 12
  [InlineData("Акбай", "Акбаевич", "Акбаевна")]
  [InlineData("Кий", "Киевич", "Киевна")]
  [InlineData("Матвей", "Матвеевич", "Матвеевна")]
  // Rule 13
  [InlineData("Бимбии", "Бимбииевич", "Бимбииевна")]
  [InlineData("Бобоо", "Бобооевич", "Бобооевна")]
  [InlineData("Бурбээ", "Бурбээевич", "Бурбээевна")]
  public void PatronymicRU(string name, string male, string female)
  {
    var malePatronymic = NameDeclension.ToMaleDeclension(Language.RU, Core.Project.Dto.NameType.FirstName, name);
    var femalePatronymic = NameDeclension.ToFemaleDeclension(Language.RU, Core.Project.Dto.NameType.FirstName, name);

    Assert.Equal(male, malePatronymic);
    Assert.Equal(female, femalePatronymic);
  }

  [Theory]
  // Rule 1
  [InlineData("Ивановы", "Иванов", "Иванова")]
  [InlineData("Кузнецовы", "Кузнецов", "Кузнецова")]
  // Rule 2
  [InlineData("Григорьевы", "Григорьев", "Григорьева")]
  // Rule 3
  [InlineData("Пушкины", "Пушкин", "Пушкина")]
  // Rule 4
  [InlineData("Голицыны", "Голицын", "Голицына")]
  [InlineData("Лисицыны", "Лисицын", "Лисицына")]
  // Rule 5
  [InlineData("Достоевские", "Достоевский", "Достоевская")]
  // Rule 6
  [InlineData("Луцкие", "Луцкий", "Луцкая")]
  // Rule 7
  [InlineData("Белые", "Белый", "Белая")]
  [InlineData("Толстые", "Толстый", "Толстая")]
  // Rule 8
  [InlineData("Дикие", "Дикий", "Дикая")]
  // Rule 9
  [InlineData("Блоки", "Блок", "Блок")]
  [InlineData("Эдельманы", "Эдельман", "Эдельман")]
  public void FamilyNameRU(string name, string male, string female)
  {
    var maleSurname = NameDeclension.ToMaleDeclension(Language.RU, Core.Project.Dto.NameType.FamilyName, name);
    var femaleSurname = NameDeclension.ToFemaleDeclension(Language.RU, Core.Project.Dto.NameType.FamilyName, name);

    Assert.Equal(male, maleSurname);
    Assert.Equal(female, femaleSurname);
  }
}


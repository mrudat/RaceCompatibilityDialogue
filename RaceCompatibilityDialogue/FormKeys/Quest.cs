// Autogenerated by https://github.com/Mutagen-Modding/Mutagen.Bethesda.FormKeys

using Mutagen.Bethesda.Skyrim;
using System.CodeDom.Compiler;

namespace Mutagen.Bethesda.FormKeys.SkyrimSE
{
    public static partial class RaceCompatibility
    {
        public static class Quest
        {
            private static FormLink<IQuestGetter> Construct(uint id) => new FormLink<IQuestGetter>(ModKey.MakeFormKey(id));
            public static FormLink<IQuestGetter> RaceDispatcher => Construct(0x9f27);
        }
    }
}

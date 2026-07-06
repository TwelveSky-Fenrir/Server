using System.Runtime.CompilerServices;

// Reader/Record types are internal; Fenrir.Tools.LegacyDataImport.Tests exercises ItemReader/MonsterReader's
// runtime-patch replay directly against the real Server/BuildEU33/DATA files.
[assembly: InternalsVisibleTo("Fenrir.Tools.LegacyDataImport.Tests")]

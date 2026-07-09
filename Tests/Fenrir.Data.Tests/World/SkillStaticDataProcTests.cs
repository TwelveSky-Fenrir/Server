using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.Tests.Fixtures;
using Fenrir.Data.World;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.World;

// world.usp_Skill_GetAll (the read path) and the 9 world.Skills / 25 world.SkillGrades CHECK constraints
// added by Database/Migrations/032_skill_static_data_range_checks.sql, against real SQL Server 2025. Neither
// table has a runtime C# write path (both are seed-only), so the CHECK-violation tests below insert directly
// via SqlCommand rather than through IWorldDataRepository -- same pattern as
// StarterKitProcTests.CreateWithStarterKitAsync_PreviousTribeOutOfRange_ThrowsCheckConstraintViolation.
[Collection("SqlServer")]
public class SkillStaticDataProcTests
{
    private readonly string _connectionString;
    private readonly IWorldDataRepository _repository;

    public SkillStaticDataProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _repository = new WorldDataRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    // Confirms the read path (world.usp_Skill_GetAll -> SkillRowDto/SkillGradeRowDto) still loads the full
    // seeded catalog correctly now that the CHECK constraints sit underneath it, and that every seeded row
    // genuinely falls inside every new bound -- not just the migration header's own prose claim.
    //
    // NOTE: Migrations/032_skill_static_data_range_checks.sql's own header claims "293 seeded skill rows and
    // 586 seeded grade rows" -- that is the highest SkillId used (070_skills.sql seeds up to SkillId 293), not
    // the row count. The actual seeded row count, confirmed against the real database here, is 153 skills (2
    // rows per skill = 306 grades), matching Database/Tables/world/Skills.sql's own comment ("147 of 300 raw
    // records are blank filler rows all sharing Index == 0" => 300 - 147 = 153). This does not affect the
    // migration's correctness (every seeded row still passes every new bound, verified below) but the header's
    // row-count prose is inaccurate and worth a follow-up correction.
    [Fact]
    public async Task GetSkillsAsync_LoadsSeededCatalog_AllRowsWithinTheNewCheckConstraintBounds()
    {
        var (skills, descriptions, grades) = await _repository.GetSkillsAsync(CancellationToken.None);

        Assert.Equal(153, skills.Count);
        Assert.Equal(306, grades.Count);
        Assert.NotEmpty(descriptions);

        foreach (var skill in skills)
        {
            Assert.InRange(skill.Type, (byte)1, (byte)4);
            Assert.InRange(skill.AttackType, (byte)1, (byte)5);
            Assert.InRange(skill.DataNumber2D, (short)1, (short)10000);
            Assert.InRange(skill.TribeInfo1, (byte)1, (byte)4);
            Assert.InRange(skill.TribeInfo2, (byte)1, (byte)10);
            Assert.InRange(skill.LearnSkillPoint, (byte)1, byte.MaxValue); // CHECK ceiling is 1000, unreachable under TINYINT's 255
            // MaxUpgradePoint's floor of 1 is the divide-by-zero guard for SKILLSYSTEM::ReturnSkillValue
            // (Server/ts25zone/GameSystem/GameSystem_03_Skill.cpp:186) -- the highest-severity bound in the
            // migration, asserted explicitly rather than folded into Assert.InRange.
            Assert.True(skill.MaxUpgradePoint >= 1,
                $"SkillId {skill.SkillId} has MaxUpgradePoint {skill.MaxUpgradePoint}, which would divide-by-zero " +
                "in SKILLSYSTEM::ReturnSkillValue.");
            Assert.InRange(skill.TotalHitNumber, (byte)0, (byte)10);
            Assert.InRange(skill.ValidRadius, (short)0, (short)1000);
        }

        foreach (var grade in grades)
        {
            Assert.InRange(grade.ManaUse, (short)0, (short)10000);
            Assert.InRange(grade.RecoverInfo1, (byte)0, byte.MaxValue); // CHECK ceiling is 10000, unreachable under TINYINT's 255
            Assert.InRange(grade.RecoverInfo2, (byte)0, byte.MaxValue);
            Assert.InRange(grade.StunAttack, (byte)0, (byte)100);
            Assert.InRange(grade.StunDefense, (byte)0, (byte)100);
            Assert.InRange(grade.FastRunSpeed, (short)0, (short)1000);
            Assert.InRange(grade.AttackInfo1, (short)0, (short)1000);
            Assert.InRange(grade.RunTime, (short)0, (short)10000);
        }
    }

    // MaxUpgradePoint is the highest-severity constraint added by this migration -- see the migration's own
    // header and the assertion above.
    [Fact]
    public async Task InsertSkill_MaxUpgradePointZero_ThrowsCheckConstraintViolation()
    {
        var ex = await Record.ExceptionAsync(() => InsertSkillAsync(NewSkillId(), maxUpgradePoint: 0));

        AssertCheckConstraintViolation(ex);
    }

    [Fact]
    public async Task InsertSkill_TypeOutOfRange_ThrowsCheckConstraintViolation()
    {
        var ex = await Record.ExceptionAsync(() => InsertSkillAsync(NewSkillId(), type: 5));

        AssertCheckConstraintViolation(ex);
    }

    [Fact]
    public async Task InsertSkill_ValidRow_Succeeds()
    {
        // Proves the two failing tests above (and the helper below) actually fail because of the single
        // overridden field, not because the baseline row is itself invalid.
        var skillId = NewSkillId();

        var ex = await Record.ExceptionAsync(() => InsertSkillAsync(skillId));

        Assert.Null(ex);
    }

    [Fact]
    public async Task InsertSkillGrade_ManaUseNegative_ThrowsCheckConstraintViolation()
    {
        var skillId = NewSkillId();
        await InsertSkillAsync(skillId);

        var ex = await Record.ExceptionAsync(() => InsertSkillGradeAsync(skillId, manaUse: -1));

        AssertCheckConstraintViolation(ex);
    }

    [Fact]
    public async Task InsertSkillGrade_StunAttackOutOfRange_ThrowsCheckConstraintViolation()
    {
        var skillId = NewSkillId();
        await InsertSkillAsync(skillId);

        var ex = await Record.ExceptionAsync(() => InsertSkillGradeAsync(skillId, stunAttack: 101));

        AssertCheckConstraintViolation(ex);
    }

    [Fact]
    public async Task InsertSkillGrade_ValidRow_Succeeds()
    {
        var skillId = NewSkillId();
        await InsertSkillAsync(skillId);

        var ex = await Record.ExceptionAsync(() => InsertSkillGradeAsync(skillId));

        Assert.Null(ex);
    }

    // The 293 seeded SkillIds top out at 293 (Migrations/Seed/world/070_skills.sql); anything from here up is
    // guaranteed unused by seed data.
    private static int _nextSkillId = 900_001;

    private static int NewSkillId()
    {
        return Interlocked.Increment(ref _nextSkillId);
    }

    private static void AssertCheckConstraintViolation(Exception? ex)
    {
        Assert.NotNull(ex);
        var sqlException = ex as SqlException ?? ex.InnerException as SqlException;
        Assert.NotNull(sqlException);
        Assert.Equal(547, sqlException!.Number);
    }

    private async Task InsertSkillAsync(
        int skillId,
        byte type = 1,
        byte attackType = 1,
        short dataNumber2D = 1,
        byte tribeInfo1 = 1,
        byte tribeInfo2 = 1,
        byte learnSkillPoint = 1,
        byte maxUpgradePoint = 10,
        byte totalHitNumber = 0,
        short validRadius = 0)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            """
            INSERT INTO world.Skills
                (SkillId, Name, Type, AttackType, DataNumber2D, TribeInfo1, TribeInfo2, LearnSkillPoint,
                 MaxUpgradePoint, TotalHitNumber, ValidRadius)
            VALUES
                (@SkillId, @Name, @Type, @AttackType, @DataNumber2D, @TribeInfo1, @TribeInfo2, @LearnSkillPoint,
                 @MaxUpgradePoint, @TotalHitNumber, @ValidRadius);
            """, connection);
        command.Parameters.AddWithValue("SkillId", skillId);
        command.Parameters.AddWithValue("Name", $"Test{skillId}");
        command.Parameters.AddWithValue("Type", type);
        command.Parameters.AddWithValue("AttackType", attackType);
        command.Parameters.AddWithValue("DataNumber2D", dataNumber2D);
        command.Parameters.AddWithValue("TribeInfo1", tribeInfo1);
        command.Parameters.AddWithValue("TribeInfo2", tribeInfo2);
        command.Parameters.AddWithValue("LearnSkillPoint", learnSkillPoint);
        command.Parameters.AddWithValue("MaxUpgradePoint", maxUpgradePoint);
        command.Parameters.AddWithValue("TotalHitNumber", totalHitNumber);
        command.Parameters.AddWithValue("ValidRadius", validRadius);
        await command.ExecuteNonQueryAsync();
    }

    private async Task InsertSkillGradeAsync(
        int skillId,
        byte gradeIndex = 0,
        short manaUse = 0,
        byte recoverInfo1 = 0,
        byte recoverInfo2 = 0,
        byte stunAttack = 0,
        byte stunDefense = 0,
        short fastRunSpeed = 0,
        short attackInfo1 = 0,
        byte attackInfo2 = 0,
        byte attackInfo3 = 0,
        short runTime = 0,
        byte chargingDamageUp = 0,
        byte attackPowerUp = 0,
        byte defensePowerUp = 0,
        byte attackSuccessUp = 0,
        byte attackBlockUp = 0,
        byte elementAttackUp = 0,
        byte elementDefenseUp = 0,
        byte attackSpeedUp = 0,
        byte runSpeedUp = 0,
        byte shieldLifeUp = 0,
        byte luckUp = 0,
        byte criticalUp = 0,
        byte returnSuccessUp = 0,
        byte stunDefenseUp = 0,
        byte destroySuccessUp = 0)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            """
            INSERT INTO world.SkillGrades
                (SkillId, GradeIndex, ManaUse, RecoverInfo1, RecoverInfo2, StunAttack, StunDefense, FastRunSpeed,
                 AttackInfo1, AttackInfo2, AttackInfo3, RunTime, ChargingDamageUp, AttackPowerUp, DefensePowerUp,
                 AttackSuccessUp, AttackBlockUp, ElementAttackUp, ElementDefenseUp, AttackSpeedUp, RunSpeedUp,
                 ShieldLifeUp, LuckUp, CriticalUp, ReturnSuccessUp, StunDefenseUp, DestroySuccessUp)
            VALUES
                (@SkillId, @GradeIndex, @ManaUse, @RecoverInfo1, @RecoverInfo2, @StunAttack, @StunDefense,
                 @FastRunSpeed, @AttackInfo1, @AttackInfo2, @AttackInfo3, @RunTime, @ChargingDamageUp,
                 @AttackPowerUp, @DefensePowerUp, @AttackSuccessUp, @AttackBlockUp, @ElementAttackUp,
                 @ElementDefenseUp, @AttackSpeedUp, @RunSpeedUp, @ShieldLifeUp, @LuckUp, @CriticalUp,
                 @ReturnSuccessUp, @StunDefenseUp, @DestroySuccessUp);
            """, connection);
        command.Parameters.AddWithValue("SkillId", skillId);
        command.Parameters.AddWithValue("GradeIndex", gradeIndex);
        command.Parameters.AddWithValue("ManaUse", manaUse);
        command.Parameters.AddWithValue("RecoverInfo1", recoverInfo1);
        command.Parameters.AddWithValue("RecoverInfo2", recoverInfo2);
        command.Parameters.AddWithValue("StunAttack", stunAttack);
        command.Parameters.AddWithValue("StunDefense", stunDefense);
        command.Parameters.AddWithValue("FastRunSpeed", fastRunSpeed);
        command.Parameters.AddWithValue("AttackInfo1", attackInfo1);
        command.Parameters.AddWithValue("AttackInfo2", attackInfo2);
        command.Parameters.AddWithValue("AttackInfo3", attackInfo3);
        command.Parameters.AddWithValue("RunTime", runTime);
        command.Parameters.AddWithValue("ChargingDamageUp", chargingDamageUp);
        command.Parameters.AddWithValue("AttackPowerUp", attackPowerUp);
        command.Parameters.AddWithValue("DefensePowerUp", defensePowerUp);
        command.Parameters.AddWithValue("AttackSuccessUp", attackSuccessUp);
        command.Parameters.AddWithValue("AttackBlockUp", attackBlockUp);
        command.Parameters.AddWithValue("ElementAttackUp", elementAttackUp);
        command.Parameters.AddWithValue("ElementDefenseUp", elementDefenseUp);
        command.Parameters.AddWithValue("AttackSpeedUp", attackSpeedUp);
        command.Parameters.AddWithValue("RunSpeedUp", runSpeedUp);
        command.Parameters.AddWithValue("ShieldLifeUp", shieldLifeUp);
        command.Parameters.AddWithValue("LuckUp", luckUp);
        command.Parameters.AddWithValue("CriticalUp", criticalUp);
        command.Parameters.AddWithValue("ReturnSuccessUp", returnSuccessUp);
        command.Parameters.AddWithValue("StunDefenseUp", stunDefenseUp);
        command.Parameters.AddWithValue("DestroySuccessUp", destroySuccessUp);
        await command.ExecuteNonQueryAsync();
    }
}

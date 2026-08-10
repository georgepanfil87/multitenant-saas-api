using MultiTenantSaaS.Domain.Entities;
using MultiTenantSaaS.Domain.Enums;
using Xunit;

namespace MultiTenantSaaS.UnitTests.Domain;

/// <summary>
/// Invarianții care trebuie să reziste indiferent de calea de intrare: controller, job de
/// import sau seed. De aceea trăiesc în entități, nu în servicii.
/// </summary>
public sealed class EntityInvariantTests
{
    [Theory]
    [InlineData("acme-corp", true)]
    [InlineData("acme", true)]
    [InlineData("a1b", true)]
    [InlineData("ab", false)]          // sub 3 caractere
    [InlineData("-acme", false)]       // nu poate începe cu cratimă
    [InlineData("acme-", false)]       // nici să se termine
    [InlineData("Acme Corp", false)]   // fără spații sau majuscule
    [InlineData("acme_corp", false)]   // underscore nu e valid în DNS
    public void Tenant_Slug_FollowsDnsLabelRules(string slug, bool isValid)
    {
        if (isValid)
        {
            Assert.Equal(slug, Tenant.Create("Acme", slug).Slug);
        }
        else
        {
            Assert.Throws<ArgumentException>(() => Tenant.Create("Acme", slug));
        }
    }

    [Fact]
    public void Tenant_Slug_IsNormalizedToLowercase()
    {
        Assert.Equal("acme-corp", Tenant.Create("Acme", "ACME-CORP").Slug);
    }

    [Fact]
    public void Tenant_NewOrganization_StartsActiveOnFreePlan()
    {
        var tenant = Tenant.Create("Acme", "acme");

        Assert.True(tenant.IsActive);
        Assert.Equal(SubscriptionPlan.Free, tenant.Plan);
        Assert.Null(tenant.RequestsPerMinuteOverride);
    }

    [Fact]
    public void Tenant_Deactivate_KeepsDataButBlocksAccess()
    {
        var tenant = Tenant.Create("Acme", "acme");
        tenant.Deactivate();

        Assert.False(tenant.IsActive);
        Assert.NotNull(tenant.UpdatedAtUtc);
    }

    [Fact]
    public void User_Email_IsNormalizedToLowercase()
    {
        // Fără normalizare, indexul unic (TenantId, Email) ar lăsa să treacă
        // „George@acme.ro" și „george@acme.ro" ca două conturi distincte.
        Assert.Equal("george@acme.ro", User.Create(" George@Acme.RO ", "hash", "George").Email);
    }

    [Fact]
    public void User_NewAccount_HasNoTenantUntilSaved()
    {
        // TenantId e ștampilat de DbContext, nu de codul de aplicație.
        Assert.Equal(Guid.Empty, User.Create("a@b.ro", "hash", "A B").TenantId);
    }

    [Fact]
    public void User_ChangeRole_RefusesGlobalAdmin()
    {
        var user = User.Create("a@b.ro", "hash", "A B");

        // Invariantul de securitate al platformei: un TenantAdmin nu se poate ridica
        // singur la administrator global, deci nu poate ieși din propriul tenant.
        Assert.Throws<InvalidOperationException>(() => user.ChangeRole(UserRole.GlobalAdmin));

        user.ChangeRole(UserRole.TenantAdmin);
        Assert.Equal(UserRole.TenantAdmin, user.Role);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void User_Create_RejectsBlankRequiredFields(string blank)
    {
        Assert.Throws<ArgumentException>(() => User.Create(blank, "hash", "Nume"));
        Assert.Throws<ArgumentException>(() => User.Create("a@b.ro", blank, "Nume"));
        Assert.Throws<ArgumentException>(() => User.Create("a@b.ro", "hash", blank));
    }

    [Fact]
    public void Project_Code_IsNormalizedToUppercase()
    {
        Assert.Equal("SUP", Project.Create("Suport", " sup ", Guid.NewGuid()).Code);
    }

    [Theory]
    [InlineData("S")]                 // prea scurt
    [InlineData("PREA-LUNG-COD")]     // peste 10 caractere
    public void Project_Code_HasLengthLimits(string code)
    {
        Assert.Throws<ArgumentException>(() => Project.Create("Proiect", code, Guid.NewGuid()));
    }

    [Fact]
    public void Project_Create_RequiresAuthor()
    {
        Assert.Throws<ArgumentException>(() => Project.Create("Proiect", "PRJ", Guid.Empty));
    }

    [Fact]
    public void Ticket_NewTicket_StartsOpenWithMediumPriority()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Titlu", Guid.NewGuid());

        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(TicketPriority.Medium, ticket.Priority);
        Assert.Null(ticket.AssignedToUserId);
        Assert.Null(ticket.ClosedAtUtc);
    }

    [Theory]
    [InlineData(TicketStatus.Open, TicketStatus.InProgress, true)]
    [InlineData(TicketStatus.Open, TicketStatus.Resolved, true)]
    [InlineData(TicketStatus.Open, TicketStatus.Closed, false)]      // nu se sare peste rezolvare
    [InlineData(TicketStatus.InProgress, TicketStatus.Open, true)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Closed, false)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Closed, true)]
    [InlineData(TicketStatus.Resolved, TicketStatus.InProgress, true)] // respins la verificare
    public void Ticket_StatusTransitions_FollowLifecycle(
        TicketStatus from, TicketStatus to, bool isAllowed)
    {
        var ticket = MoveTo(from);

        if (isAllowed)
        {
            ticket.ChangeStatus(to);
            Assert.Equal(to, ticket.Status);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => ticket.ChangeStatus(to));
        }
    }

    [Fact]
    public void Ticket_ClosedAtUtc_IsSetOnCloseAndClearedOnReopen()
    {
        var ticket = MoveTo(TicketStatus.Closed);
        Assert.NotNull(ticket.ClosedAtUtc);

        ticket.ChangeStatus(TicketStatus.Open);
        Assert.Null(ticket.ClosedAtUtc);
    }

    [Fact]
    public void Ticket_ChangeStatus_ToSameStatus_IsNoOp()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Titlu", Guid.NewGuid());

        ticket.ChangeStatus(TicketStatus.Open);

        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Null(ticket.UpdatedAtUtc);
    }

    [Fact]
    public void Ticket_AssignTo_RejectsEmptyGuid()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Titlu", Guid.NewGuid());

        // Guid.Empty ar fi o alocare către „nimeni" care arată ca o alocare reală.
        Assert.Throws<ArgumentException>(() => ticket.AssignTo(Guid.Empty));

        ticket.AssignTo(null);
        Assert.Null(ticket.AssignedToUserId);
    }

    private static Ticket MoveTo(TicketStatus target)
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Titlu", Guid.NewGuid());

        foreach (var step in target switch
                 {
                     TicketStatus.Open => Array.Empty<TicketStatus>(),
                     TicketStatus.InProgress => [TicketStatus.InProgress],
                     TicketStatus.Resolved => [TicketStatus.InProgress, TicketStatus.Resolved],
                     TicketStatus.Closed => [TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Closed],
                     _ => Array.Empty<TicketStatus>()
                 })
        {
            ticket.ChangeStatus(step);
        }

        return ticket;
    }
}

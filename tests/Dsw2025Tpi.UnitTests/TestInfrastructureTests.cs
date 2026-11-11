using FluentAssertions;
using Xunit;

namespace Dsw2025Tpi.UnitTests;

public class TestInfrastructureTests
{
    [Fact]
    public void TestProject_IsDiscoveredAndCanRunAssertions()
    {
        // Verificacion inicial de infraestructura; los casos de negocio van en CARD-TP4-02.
        var result = 2 + 2;

        result.Should().Be(4);
    }
}

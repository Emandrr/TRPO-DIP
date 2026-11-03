using FluentAssertions;
using SaEkg.Domain;
using Xunit;

namespace SaEkg.Tests.Unit;

public class DomainMarkerTests
{
    [Fact]
    public void Domain_assembly_marker_exists()
    {
        typeof(DomainAssemblyMarker).Assembly.GetName().Name.Should().Be("SaEkg.Domain");
    }
}

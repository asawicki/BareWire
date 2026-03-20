using AwesomeAssertions;
using BareWire.Abstractions.Configuration;
using BareWire.Abstractions.Saga;
using BareWire.Transport.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BareWire.UnitTests.Core.Bus;

// ── Minimal saga state machine types for this test ────────────────────────────

public sealed class SagaBindingTestState : ISagaState
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = "Initial";
    public int Version { get; set; }
}

public sealed class SagaBindingTestMachine : BareWireStateMachine<SagaBindingTestState>
{
    public SagaBindingTestMachine()
    {
        CorrelateBy<SagaBindingTestEvent>(e => e.Id);
    }
}

public sealed record SagaBindingTestEvent(Guid Id);

// ── Tests ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Verifies that saga types registered via <c>StateMachineSaga&lt;T&gt;()</c> on a receive endpoint
/// are correctly flowed through <see cref="EndpointBinding.SagaTypes"/>.
/// Regression test for: saga types lost during config → binding conversion.
/// </summary>
public sealed class SagaEndpointBindingTests
{
    [Fact]
    public void EndpointBinding_WithSagaTypes_CarriesSagaTypesList()
    {
        // Arrange
        var sagaType = typeof(SagaBindingTestMachine);
        var binding = new EndpointBinding
        {
            EndpointName = "test-queue",
            SagaTypes = [sagaType],
        };

        // Assert
        binding.SagaTypes.Should().HaveCount(1);
        binding.SagaTypes.Should().Contain(sagaType);
    }

    [Fact]
    public void EndpointBinding_WithNoSagaTypes_HasEmptyList()
    {
        // Arrange
        var binding = new EndpointBinding
        {
            EndpointName = "test-queue",
        };

        // Assert
        binding.SagaTypes.Should().BeEmpty();
    }

    [Fact]
    public void AddBareWireRabbitMq_StateMachineSaga_FlowsSagaTypeIntoBinding()
    {
        // Arrange — configure transport with a StateMachineSaga on an endpoint
        var services = new ServiceCollection();
        services.AddBareWireRabbitMq(cfg =>
        {
            cfg.Host("amqp://guest:guest@localhost:5672/");
            cfg.ReceiveEndpoint("saga-queue", e =>
            {
                e.StateMachineSaga<SagaBindingTestMachine>();
            });
        });

        // Act — resolve the endpoint bindings
        IReadOnlyList<EndpointBinding> bindings =
            services.BuildServiceProvider().GetRequiredService<IReadOnlyList<EndpointBinding>>();

        // Assert
        bindings.Should().HaveCount(1);
        EndpointBinding binding = bindings[0];
        binding.EndpointName.Should().Be("saga-queue");
        binding.SagaTypes.Should().HaveCount(1);
        binding.SagaTypes[0].Should().Be<SagaBindingTestMachine>();
    }

    [Fact]
    public void AddBareWireRabbitMq_MultipleEndpoints_SagaTypesIsolatedPerEndpoint()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBareWireRabbitMq(cfg =>
        {
            cfg.Host("amqp://guest:guest@localhost:5672/");
            cfg.ReceiveEndpoint("saga-queue", e =>
            {
                e.StateMachineSaga<SagaBindingTestMachine>();
            });
            cfg.ReceiveEndpoint("plain-queue", e =>
            {
                // No sagas on this endpoint
            });
        });

        // Act
        IReadOnlyList<EndpointBinding> bindings =
            services.BuildServiceProvider().GetRequiredService<IReadOnlyList<EndpointBinding>>();

        // Assert
        EndpointBinding sagaBinding = bindings.Single(b => b.EndpointName == "saga-queue");
        EndpointBinding plainBinding = bindings.Single(b => b.EndpointName == "plain-queue");

        sagaBinding.SagaTypes.Should().HaveCount(1);
        plainBinding.SagaTypes.Should().BeEmpty();
    }
}

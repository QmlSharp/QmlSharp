using QmlSharp.Compiler.Tests.Fixtures;

namespace QmlSharp.Compiler.Tests.Ids
{
    public sealed class IdAllocatorTests
    {
        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void IdAllocator_ID01_AllocateMemberId_IsDeterministic()
        {
            IdAllocator first = new();
            IdAllocator second = new();

            Assert.Equal(1975080775, first.AllocateMemberId("CounterViewModel", "Count"));
            Assert.Equal(first.AllocateMemberId("CounterViewModel", "Count"), second.AllocateMemberId("CounterViewModel", "Count"));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void IdAllocator_ID02_AllocateCommandId_IsDeterministicAndDiffersFromMemberId()
        {
            IdAllocator allocator = new();

            int memberId = allocator.AllocateMemberId("CounterViewModel", "Increment");
            int commandId = allocator.AllocateCommandId("CounterViewModel", "Increment");

            Assert.Equal(985427602, commandId);
            Assert.NotEqual(memberId, commandId);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void IdAllocator_ID03_AllocateEffectId_IsDeterministic()
        {
            IdAllocator allocator = new();

            Assert.Equal(118435790, allocator.AllocateEffectId("TodoViewModel", "ShowToast"));
            Assert.Equal(118435790, allocator.AllocateEffectId("TodoViewModel", "ShowToast"));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void IdAllocator_ID04_SameInput_IsStableAcrossRuns()
        {
            IdAllocator first = new();
            IdAllocator second = new();

            Assert.Equal(first.AllocateCommandId("LoginViewModel", "Submit"), second.AllocateCommandId("LoginViewModel", "Submit"));
            Assert.Equal(first.AllocateEffectId("LoginViewModel", "LoginFailed"), second.AllocateEffectId("LoginViewModel", "LoginFailed"));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void IdAllocator_ID05_DifferentInputs_Differ()
        {
            IdAllocator allocator = new();

            Assert.NotEqual(
                allocator.AllocateMemberId("CounterViewModel", "Count"),
                allocator.AllocateMemberId("CounterViewModel", "Total"));
            Assert.NotEqual(
                allocator.AllocateCommandId("CounterViewModel", "Increment"),
                allocator.AllocateCommandId("CounterViewModel", "Reset"));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void IdAllocator_ID06_GenerateSlotKey_UsesV2SlotFormat()
        {
            IdAllocator allocator = new();

            Assert.Equal("CounterView::__qmlsharp_vm0", allocator.GenerateSlotKey("CounterView", 0));
            Assert.Equal("CounterView::__qmlsharp_vm1", allocator.GenerateSlotKey("CounterView", 1));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void IdAllocator_ID07_ComputeHash_MatchesKnownFnv1aVectors()
        {
            IdAllocator allocator = new();

            Assert.Equal(18652613, allocator.ComputeHash(string.Empty));
            Assert.Equal(1678518572, allocator.ComputeHash("a"));
            Assert.Equal(1335831723, allocator.ComputeHash("hello"));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void IdAllocator_CollisionStrategy_LinearProbesWithinSharedHashNamespace()
        {
            IdAllocator allocator = new();

            int first = allocator.AllocateCommandId("VM", "method228598");
            int second = allocator.AllocateCommandId("VM", "method800716");

            Assert.Equal(first + 1, second);
        }
    }
}

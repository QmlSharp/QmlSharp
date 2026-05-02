using Microsoft.CodeAnalysis;
using QmlSharp.Compiler.Tests.Fixtures;
using QmlSharp.Qml.Ast;
using QmlSharp.Qml.Emitter;

namespace QmlSharp.Compiler.Tests.PostProcessing
{
    public sealed class PostProcessorTests
    {
        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PP_01_InjectsDeterministicViewModelInstanceBlock()
        {
            PostProcessResult result = Process(CompilerTestFixtures.CreateCounterAstFixture(), CompilerTestFixtures.CreateCounterSchema());

            ObjectDefinitionNode viewModel = AssertSingleObject(result.Document.RootObject, "CounterViewModel");
            Assert.Contains(viewModel.Members.OfType<IdAssignmentNode>(), id => string.Equals(id.Id, "__qmlsharp_vm0", StringComparison.Ordinal));
            Assert.Contains(result.InjectedNodes, node => string.Equals(node.Kind, "ViewModelInstance", StringComparison.Ordinal));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PP_02_InjectsModuleImportAndPreservesOriginalImports()
        {
            QmlDocument document = CompilerTestFixtures.CreateCounterAstFixture() with
            {
                Imports = ImmutableArray.Create(new ImportNode
                {
                    ImportKind = ImportKind.Module,
                    ModuleUri = "QtQuick",
                    Version = "6.11",
                }),
            };

            PostProcessResult result = Process(document, CompilerTestFixtures.CreateCounterSchema());

            Assert.Contains(result.Document.Imports, import => string.Equals(import.ModuleUri, "QtQuick", StringComparison.Ordinal) && string.Equals(import.Version, "6.11", StringComparison.Ordinal));
            Assert.Contains(result.Document.Imports, import => string.Equals(import.ModuleUri, "QmlSharp.TestApp", StringComparison.Ordinal) && string.Equals(import.Version, "1.0", StringComparison.Ordinal));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PP_03_RewritesStatePropertyReferences()
        {
            PostProcessResult result = Process(CompilerTestFixtures.CreateCounterAstFixture(), CompilerTestFixtures.CreateCounterSchema());

            BindingNode textBinding = FindBinding(result.Document.RootObject, "text");
            ScriptExpression expression = Assert.IsType<ScriptExpression>(textBinding.Value);
            Assert.Equal("__qmlsharp_vm0.count.toString()", expression.Code);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PostProcessor_StateRewrite_DoesNotRewriteLocalIdentifiers()
        {
            QmlDocument document = DocumentWithMembers(new BindingNode
            {
                PropertyName = "text",
                Value = Values.Block("let count = 0;\ncount + Vm.Count;"),
            });

            PostProcessResult result = Process(document, CompilerTestFixtures.CreateCounterSchema());

            BindingNode textBinding = FindBinding(result.Document.RootObject, "text");
            ScriptBlock block = Assert.IsType<ScriptBlock>(textBinding.Value);
            Assert.Contains("let count = 0;", block.Code, StringComparison.Ordinal);
            Assert.Contains("count + __qmlsharp_vm0.count;", block.Code, StringComparison.Ordinal);
            Assert.DoesNotContain("let __qmlsharp_vm0.count", block.Code, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PP_04_RewritesCommandHandlerReferences()
        {
            QmlDocument document = DocumentWithMembers(new ObjectDefinitionNode
            {
                TypeName = "Button",
                Members = ImmutableArray.Create<AstNode>(new SignalHandlerNode
                {
                    HandlerName = "onClicked",
                    Form = SignalHandlerForm.Block,
                    Code = "Vm.Increment()",
                }),
            });

            PostProcessResult result = Process(document, CompilerTestFixtures.CreateCounterSchema());

            SignalHandlerNode handler = FindSignalHandler(result.Document.RootObject, "onClicked");
            Assert.Equal("__qmlsharp_vm0.increment()", handler.Code);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PP_05_InjectsEffectRoutingForV2Signals()
        {
            PostProcessResult result = Process(CompilerTestFixtures.CreateCounterAstFixture(), CompilerTestFixtures.CreateTodoSchema());

            ObjectDefinitionNode connections = AssertSingleObject(result.Document.RootObject, "Connections");
            BindingNode target = connections.Members.OfType<BindingNode>().Single(binding => string.Equals(binding.PropertyName, "target", StringComparison.Ordinal));
            ScriptExpression targetExpression = Assert.IsType<ScriptExpression>(target.Value);
            FunctionDeclarationNode router = connections.Members.OfType<FunctionDeclarationNode>().Single(function => string.Equals(function.Name, "onEffectDispatched", StringComparison.Ordinal));

            Assert.Equal("__qmlsharp_vm0", targetExpression.Code);
            Assert.Contains("showToast", router.Body, StringComparison.Ordinal);
            Assert.Contains("__qmlsharp_effect_router", router.Body, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PP_06_InjectsLifecycleHookCalls()
        {
            ViewModelSchema schema = CompilerTestFixtures.CreateCounterSchema() with
            {
                Lifecycle = new LifecycleInfo(OnMounted: true, OnUnmounting: true, HotReload: true),
            };

            PostProcessResult result = Process(CompilerTestFixtures.CreateCounterAstFixture(), schema);

            AttachedBindingNode component = result.Document.RootObject.Members.OfType<AttachedBindingNode>().Single(node => string.Equals(node.AttachedTypeName, "Component", StringComparison.Ordinal));
            Assert.Contains(component.Bindings, binding => string.Equals(binding.PropertyName, "onCompleted", StringComparison.Ordinal) && string.Equals(BindingCode(binding), "__qmlsharp_vm0.onMounted();", StringComparison.Ordinal));
            Assert.Contains(component.Bindings, binding => string.Equals(binding.PropertyName, "onDestruction", StringComparison.Ordinal) && string.Equals(BindingCode(binding), "__qmlsharp_vm0.onUnmounting();", StringComparison.Ordinal));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PostProcessor_LifecycleInjection_ComposesWithExistingComponentHandlers()
        {
            ViewModelSchema schema = CompilerTestFixtures.CreateCounterSchema() with
            {
                Lifecycle = new LifecycleInfo(OnMounted: true, OnUnmounting: false, HotReload: true),
            };
            QmlDocument document = DocumentWithMembers(new AttachedBindingNode
            {
                AttachedTypeName = "Component",
                Bindings = ImmutableArray.Create(new BindingNode
                {
                    PropertyName = "onCompleted",
                    Value = Values.Block("console.log(\"ready\");"),
                }),
            });

            PostProcessResult result = Process(document, schema);

            AttachedBindingNode component = result.Document.RootObject.Members.OfType<AttachedBindingNode>().Single(node => string.Equals(node.AttachedTypeName, "Component", StringComparison.Ordinal));
            BindingNode completed = Assert.Single(component.Bindings.Where(binding => string.Equals(binding.PropertyName, "onCompleted", StringComparison.Ordinal)));
            string code = BindingCode(completed);
            Assert.Contains("console.log(\"ready\");", code, StringComparison.Ordinal);
            Assert.Contains("__qmlsharp_vm0.onMounted();", code, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PostProcessor_QtQmlImport_UsesResolvedRuntimeVersion()
        {
            ViewModelSchema schema = CompilerTestFixtures.CreateCounterSchema() with
            {
                Lifecycle = new LifecycleInfo(OnMounted: true, OnUnmounting: false, HotReload: true),
            };
            ResolvedImport qtQmlImport = new("QmlSharp.QtQml", "QtQml", new QmlVersion(6, 11), Alias: null);
            PostProcessor processor = new();

            PostProcessResult result = processor.Process(
                CompilerTestFixtures.CreateCounterAstFixture(),
                CreateView(),
                schema,
                ImmutableArray.Create(qtQmlImport),
                CompilerTestFixtures.DefaultOptions);

            ImportNode qtQml = Assert.Single(result.Document.Imports.Where(import => string.Equals(import.ModuleUri, "QtQml", StringComparison.Ordinal)));
            Assert.Equal("6.11", qtQml.Version);
            Assert.DoesNotContain(result.Diagnostics, diagnostic => string.Equals(diagnostic.Code, DiagnosticCodes.ImportConflict, StringComparison.Ordinal));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PP_07_TracksInjectedNodeMetadata()
        {
            ViewModelSchema schema = CompilerTestFixtures.CreateTodoSchema() with
            {
                Lifecycle = new LifecycleInfo(OnMounted: true, OnUnmounting: false, HotReload: true),
            };

            PostProcessResult result = Process(CompilerTestFixtures.CreateCounterAstFixture(), schema);

            Assert.Contains(result.InjectedNodes, node => string.Equals(node.Kind, "ViewModelInstance", StringComparison.Ordinal));
            Assert.Contains(result.InjectedNodes, node => string.Equals(node.Kind, "ImportStatement", StringComparison.Ordinal) && string.Equals(node.Description, "QmlSharp.TestApp", StringComparison.Ordinal));
            Assert.Contains(result.InjectedNodes, node => string.Equals(node.Kind, "EffectHandler", StringComparison.Ordinal));
            Assert.Contains(result.InjectedNodes, node => string.Equals(node.Kind, "LifecycleHook", StringComparison.Ordinal));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PP_08_DetectsViewModelInstanceConflicts()
        {
            QmlDocument document = DocumentWithMembers(new ObjectDefinitionNode
            {
                TypeName = "CounterViewModel",
                Members = ImmutableArray.Create<AstNode>(new IdAssignmentNode { Id = "customVm" }),
            });

            PostProcessResult result = Process(document, CompilerTestFixtures.CreateCounterSchema());

            AssertDiagnostic(result, DiagnosticCodes.ViewModelInstanceConflict);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PP_09_DetectsImportConflicts()
        {
            QmlDocument document = CompilerTestFixtures.CreateCounterAstFixture() with
            {
                Imports = ImmutableArray.Create(new ImportNode
                {
                    ImportKind = ImportKind.Module,
                    ModuleUri = "QmlSharp.TestApp",
                    Version = "9.9",
                }),
            };

            PostProcessResult result = Process(document, CompilerTestFixtures.CreateCounterSchema());

            AssertDiagnostic(result, DiagnosticCodes.ImportConflict);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PP_10_DetectsBindingTargetNotFound()
        {
            QmlDocument document = DocumentWithMembers(new BindingNode
            {
                PropertyName = "text",
                Value = Values.Expression("Vm.Missing"),
            });

            PostProcessResult result = Process(document, CompilerTestFixtures.CreateCounterSchema());

            AssertDiagnostic(result, DiagnosticCodes.BindingTargetNotFound);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PP_11_DetectsCommandTargetNotFound()
        {
            QmlDocument document = DocumentWithMembers(new SignalHandlerNode
            {
                HandlerName = "onClicked",
                Form = SignalHandlerForm.Block,
                Code = "Vm.Missing()",
            });

            PostProcessResult result = Process(document, CompilerTestFixtures.CreateCounterSchema());

            AssertDiagnostic(result, DiagnosticCodes.CommandTargetNotFound);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PP_12_DetectsSlotKeyCollisions()
        {
            QmlDocument document = DocumentWithMembers(
                new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members = ImmutableArray.Create<AstNode>(new IdAssignmentNode { Id = "__qmlsharp_vm0" }),
                },
                new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members = ImmutableArray.Create<AstNode>(new IdAssignmentNode { Id = "__qmlsharp_vm0" }),
                });

            PostProcessResult result = Process(document, CompilerTestFixtures.CreateCounterSchema());

            AssertDiagnostic(result, DiagnosticCodes.SlotKeyCollision);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PP_13_DetectsEffectHandlerConflicts()
        {
            QmlDocument document = DocumentWithMembers(new ObjectDefinitionNode
            {
                TypeName = "Connections",
                Members = ImmutableArray.Create<AstNode>(
                    new BindingNode
                    {
                        PropertyName = "target",
                        Value = Values.Expression("__qmlsharp_vm0"),
                    },
                    new FunctionDeclarationNode
                    {
                        Name = "onEffectDispatched",
                        Body = "console.log(effectName)",
                    }),
            });

            PostProcessResult result = Process(document, CompilerTestFixtures.CreateTodoSchema());

            AssertDiagnostic(result, DiagnosticCodes.EffectHandlerConflict);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PP_14_IsIdempotentWhenProcessingRepeatedly()
        {
            PostProcessor processor = new();
            QmlDocument document = CompilerTestFixtures.CreateCounterAstFixture();
            ViewModelSchema schema = CompilerTestFixtures.CreateTodoSchema() with
            {
                Lifecycle = new LifecycleInfo(OnMounted: true, OnUnmounting: true, HotReload: true),
            };

            PostProcessResult first = processor.Process(document, CreateView(), schema, ImmutableArray<ResolvedImport>.Empty, CompilerTestFixtures.DefaultOptions);
            PostProcessResult second = processor.Process(first.Document, CreateView(), schema, ImmutableArray<ResolvedImport>.Empty, CompilerTestFixtures.DefaultOptions);

            _ = Assert.Single(second.Document.RootObject.Members.OfType<ObjectDefinitionNode>(), node => string.Equals(node.TypeName, "TodoViewModel", StringComparison.Ordinal));
            _ = Assert.Single(second.Document.RootObject.Members.OfType<ObjectDefinitionNode>(), node => string.Equals(node.TypeName, "Connections", StringComparison.Ordinal));
            _ = Assert.Single(second.Document.RootObject.Members.OfType<AttachedBindingNode>(), node => string.Equals(node.AttachedTypeName, "Component", StringComparison.Ordinal));
            Assert.DoesNotContain(second.Diagnostics, diagnostic => string.Equals(diagnostic.Code, DiagnosticCodes.EffectHandlerConflict, StringComparison.Ordinal));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PP_V2_DoesNotEmitV1OrQmlTsGlue()
        {
            QmlDocument document = DocumentWithMembers(
                new BindingNode
                {
                    PropertyName = "text",
                    Value = Values.Expression("vm.Count"),
                },
                new SignalHandlerNode
                {
                    HandlerName = "onClicked",
                    Form = SignalHandlerForm.Block,
                    Code = "vm.Increment()",
                });
            PostProcessResult result = Process(document, CompilerTestFixtures.CreateCounterSchema());
            QmlEmitter emitter = new();

            string output = emitter.Emit(result.Document);

            Assert.DoesNotContain("__qmlts", output, StringComparison.Ordinal);
            Assert.DoesNotContain("setContextProperty", output, StringComparison.Ordinal);
            Assert.DoesNotContain("contextProperty", output, StringComparison.Ordinal);
            Assert.DoesNotContain("vm.", output, StringComparison.Ordinal);
            Assert.Contains("__qmlsharp_vm0.count", output, StringComparison.Ordinal);
            Assert.Contains("__qmlsharp_vm0.increment()", output, StringComparison.Ordinal);
        }

        private static PostProcessResult Process(QmlDocument document, ViewModelSchema schema)
        {
            PostProcessor processor = new();
            return processor.Process(document, CreateView(), schema, ImmutableArray<ResolvedImport>.Empty, CompilerTestFixtures.DefaultOptions);
        }

        private static DiscoveredView CreateView()
        {
            ProjectContext context = CompilerTestFixtures.CreateCounterContext();
            INamedTypeSymbol viewSymbol = context.Compilation.GetTypeByMetadataName("TestApp.CounterView")
                ?? throw new InvalidOperationException("CounterView test symbol was not found.");
            INamedTypeSymbol viewModelSymbol = context.Compilation.GetTypeByMetadataName("TestApp.CounterViewModel")
                ?? throw new InvalidOperationException("CounterViewModel test symbol was not found.");

            return new DiscoveredView("CounterView", "CounterView.cs", "CounterViewModel", viewSymbol, viewModelSymbol);
        }

        private static QmlDocument DocumentWithMembers(params AstNode[] members)
        {
            return new QmlDocument
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members = members.ToImmutableArray(),
                },
            };
        }

        private static ObjectDefinitionNode AssertSingleObject(ObjectDefinitionNode root, string typeName)
        {
            return root.Members.OfType<ObjectDefinitionNode>().Single(node => string.Equals(node.TypeName, typeName, StringComparison.Ordinal));
        }

        private static BindingNode FindBinding(ObjectDefinitionNode node, string propertyName)
        {
            if (TryFindBinding(node, propertyName, out BindingNode? binding) && binding is not null)
            {
                return binding;
            }

            throw new InvalidOperationException($"Binding '{propertyName}' was not found.");
        }

        private static bool TryFindBinding(ObjectDefinitionNode node, string propertyName, out BindingNode? binding)
        {
            foreach (BindingNode candidate in node.Members
                .OfType<BindingNode>()
                .Where(candidate => string.Equals(candidate.PropertyName, propertyName, StringComparison.Ordinal)))
            {
                binding = candidate;
                return true;
            }

            foreach (ObjectDefinitionNode child in node.Members
                .Where(member => member is ObjectDefinitionNode)
                .Cast<ObjectDefinitionNode>())
            {
                if (TryFindBinding(child, propertyName, out binding))
                {
                    return true;
                }
            }

            binding = null;
            return false;
        }

        private static SignalHandlerNode FindSignalHandler(ObjectDefinitionNode node, string handlerName)
        {
            if (TryFindSignalHandler(node, handlerName, out SignalHandlerNode? handler) && handler is not null)
            {
                return handler;
            }

            throw new InvalidOperationException($"Handler '{handlerName}' was not found.");
        }

        private static bool TryFindSignalHandler(ObjectDefinitionNode node, string handlerName, out SignalHandlerNode? handler)
        {
            foreach (SignalHandlerNode candidate in node.Members
                .OfType<SignalHandlerNode>()
                .Where(candidate => string.Equals(candidate.HandlerName, handlerName, StringComparison.Ordinal)))
            {
                handler = candidate;
                return true;
            }

            foreach (ObjectDefinitionNode child in node.Members
                .Where(member => member is ObjectDefinitionNode)
                .Cast<ObjectDefinitionNode>())
            {
                if (TryFindSignalHandler(child, handlerName, out handler))
                {
                    return true;
                }
            }

            handler = null;
            return false;
        }

        private static string BindingCode(BindingNode binding)
        {
            return binding.Value switch
            {
                ScriptExpression expression => expression.Code,
                ScriptBlock block => block.Code,
                _ => throw new InvalidOperationException("Expected script binding value."),
            };
        }

        private static void AssertDiagnostic(PostProcessResult result, string code)
        {
            Assert.Contains(result.Diagnostics, diagnostic => string.Equals(diagnostic.Code, code, StringComparison.Ordinal));
        }
    }
}

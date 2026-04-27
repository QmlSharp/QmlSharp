using System.Collections.Immutable;
using QmlSharp.Qml.Ast.Traversal;

#pragma warning disable MA0048

namespace QmlSharp.Qml.Ast.Utilities
{
    /// <summary>
    /// Static utility methods for common deterministic AST queries.
    /// </summary>
    public static class QmlAstUtils
    {
        /// <summary>
        /// Collects every id assignment in document traversal order.
        /// </summary>
        /// <param name="document">Document to query.</param>
        /// <returns>Id values in declaration order.</returns>
        public static ImmutableArray<string> CollectIds(QmlDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            ImmutableArray<string>.Builder ids = ImmutableArray.CreateBuilder<string>();
            QmlAstWalker.Walk(
                document,
                enter: (node, _) =>
                {
                    if (node is IdAssignmentNode idAssignmentNode)
                    {
                        ids.Add(idAssignmentNode.Id);
                    }

                    return true;
                },
                leave: null);

            return ids.ToImmutable();
        }

        /// <summary>
        /// Collects referenced type names in first-seen traversal order.
        /// </summary>
        /// <param name="document">Document to query.</param>
        /// <returns>Distinct referenced type names.</returns>
        public static ImmutableArray<string> CollectTypeNames(QmlDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            ImmutableArray<string>.Builder typeNames = ImmutableArray.CreateBuilder<string>();
            HashSet<string> seen = new(StringComparer.Ordinal);
            QmlAstWalker.Walk(
                document,
                enter: (node, _) =>
                {
                    CollectTypeNamesFromNode(node, seen, typeNames);
                    return true;
                },
                leave: null);

            return typeNames.ToImmutable();
        }

        /// <summary>
        /// Collects module import URIs in import declaration order.
        /// </summary>
        /// <param name="document">Document to query.</param>
        /// <returns>Module URIs for module imports only.</returns>
        public static ImmutableArray<string> CollectImportedModules(QmlDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            ImmutableArray<string>.Builder modules = ImmutableArray.CreateBuilder<string>();
            foreach (ImportNode importNode in document.Imports.Where(
                static importNode => importNode.ImportKind == ImportKind.Module && importNode.ModuleUri is not null))
            {
                modules.Add(importNode.ModuleUri!);
            }

            return modules.ToImmutable();
        }

        /// <summary>
        /// Finds all object definitions with a matching type name.
        /// </summary>
        /// <param name="document">Document to query.</param>
        /// <param name="typeName">Type name to match using ordinal comparison.</param>
        /// <returns>Matching objects in traversal order.</returns>
        public static ImmutableArray<ObjectDefinitionNode> FindObjectsByType(QmlDocument document, string typeName)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(typeName);

            ImmutableArray<ObjectDefinitionNode>.Builder matches = ImmutableArray.CreateBuilder<ObjectDefinitionNode>();
            QmlAstWalker.Walk(
                document,
                enter: (node, _) =>
                {
                    if (node is ObjectDefinitionNode objectDefinitionNode
                        && string.Equals(objectDefinitionNode.TypeName, typeName, StringComparison.Ordinal))
                    {
                        matches.Add(objectDefinitionNode);
                    }

                    return true;
                },
                leave: null);

            return matches.ToImmutable();
        }

        /// <summary>
        /// Finds the first object definition that contains the requested id assignment.
        /// </summary>
        /// <param name="document">Document to query.</param>
        /// <param name="id">Id to match using ordinal comparison.</param>
        /// <returns>The containing object definition, or null when no match exists.</returns>
        public static ObjectDefinitionNode? FindObjectById(QmlDocument document, string id)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(id);

            ObjectDefinitionNode? match = null;
            QmlAstWalker.Walk(
                document,
                enter: (node, context) =>
                {
                    if (match is not null)
                    {
                        return false;
                    }

                    if (node is IdAssignmentNode idAssignmentNode
                        && string.Equals(idAssignmentNode.Id, id, StringComparison.Ordinal)
                        && context.Parent is ObjectDefinitionNode objectDefinitionNode)
                    {
                        match = objectDefinitionNode;
                    }

                    return true;
                },
                leave: null);

            return match;
        }

        /// <summary>
        /// Gets the id assigned directly to an object definition.
        /// </summary>
        /// <param name="obj">Object to query.</param>
        /// <returns>The direct id value, or null when the object has no id assignment.</returns>
        public static string? GetObjectId(ObjectDefinitionNode obj)
        {
            ArgumentNullException.ThrowIfNull(obj);

            return obj.Members
                .OfType<IdAssignmentNode>()
                .FirstOrDefault()?
                .Id;
        }

        /// <summary>
        /// Gets the direct simple binding value for a named property on an object.
        /// </summary>
        /// <param name="obj">Object to query.</param>
        /// <param name="propertyName">Property name to match using ordinal comparison.</param>
        /// <returns>The binding value, or null when no direct simple binding exists.</returns>
        public static BindingValue? GetBindingValue(ObjectDefinitionNode obj, string propertyName)
        {
            ArgumentNullException.ThrowIfNull(obj);
            ArgumentNullException.ThrowIfNull(propertyName);

            return obj.Members
                .OfType<BindingNode>()
                .Where(bindingNode => string.Equals(bindingNode.PropertyName, propertyName, StringComparison.Ordinal))
                .Select(static bindingNode => bindingNode.Value)
                .FirstOrDefault();
        }

        /// <summary>
        /// Gets immediate AST children using the same child ordering as walker traversal.
        /// </summary>
        /// <param name="node">Node to query.</param>
        /// <returns>Immediate child nodes.</returns>
        public static ImmutableArray<AstNode> GetChildren(AstNode node)
        {
            ArgumentNullException.ThrowIfNull(node);

            ImmutableArray<AstNode>.Builder children = ImmutableArray.CreateBuilder<AstNode>();
            AddLeadingComments(node, children);
            AddStructuralChildren(node, children);
            AddTrailingComment(node, children);
            return children.ToImmutable();
        }

        /// <summary>
        /// Counts all AST nodes in a document, including the document node itself.
        /// </summary>
        /// <param name="document">Document to count.</param>
        /// <returns>Total AST node count.</returns>
        public static int CountNodes(QmlDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            int count = 0;
            QmlAstWalker.Walk(
                document,
                enter: (_, _) =>
                {
                    count++;
                    return true;
                },
                leave: null);

            return count;
        }

        /// <summary>
        /// Computes maximum object nesting depth. Document depth is 0 and root object depth is 1.
        /// </summary>
        /// <param name="document">Document to inspect.</param>
        /// <returns>Maximum object nesting depth.</returns>
        public static int MaxDepth(QmlDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            return MaxObjectDepth(document.RootObject, 1);
        }

        /// <summary>
        /// Tests whether two AST nodes are structurally equal.
        /// </summary>
        /// <param name="a">First node.</param>
        /// <param name="b">Second node.</param>
        /// <param name="ignoreSpan">True to ignore source span differences; false to compare spans.</param>
        /// <returns>True when both nodes have equal structure and values.</returns>
        public static bool StructuralEqual(AstNode a, AstNode b, bool ignoreSpan = true)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);

            return NodesEqual(a, b, ignoreSpan);
        }

        /// <summary>
        /// Produces a short deterministic summary for diagnostics and tests.
        /// </summary>
        /// <param name="document">Document to summarize.</param>
        /// <returns>Human-readable document summary.</returns>
        public static string Summarize(QmlDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            string rootId = GetObjectId(document.RootObject) ?? "<none>";
            return string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"QmlDocument({document.Pragmas.Length} pragmas, {document.Imports.Length} imports, root={document.RootObject.TypeName}, id={rootId}, members={document.RootObject.Members.Length}, nodes={CountNodes(document)}, maxDepth={MaxDepth(document)})");
        }

        private static void CollectTypeNamesFromNode(
            AstNode node,
            HashSet<string> seen,
            ImmutableArray<string>.Builder typeNames)
        {
            switch (node)
            {
                case ObjectDefinitionNode objectDefinitionNode:
                    AddTypeName(objectDefinitionNode.TypeName, seen, typeNames);
                    break;

                case InlineComponentNode inlineComponentNode:
                    AddTypeName(inlineComponentNode.Name, seen, typeNames);
                    break;

                case PropertyDeclarationNode propertyDeclarationNode:
                    CollectTypeNamesFromPropertyDeclaration(propertyDeclarationNode, seen, typeNames);
                    break;

                case BindingNode bindingNode:
                    CollectTypeNamesFromValue(bindingNode.Value, seen, typeNames);
                    break;

                case GroupedBindingNode groupedBindingNode:
                    CollectTypeNamesFromBindingList(groupedBindingNode.Bindings, seen, typeNames);
                    break;

                case AttachedBindingNode attachedBindingNode:
                    AddTypeName(attachedBindingNode.AttachedTypeName, seen, typeNames);
                    CollectTypeNamesFromBindingList(attachedBindingNode.Bindings, seen, typeNames);
                    break;

                case ArrayBindingNode arrayBindingNode:
                    CollectTypeNamesFromValueList(arrayBindingNode.Elements, seen, typeNames);
                    break;

                case SignalDeclarationNode signalDeclarationNode:
                    AddParameterTypeNames(signalDeclarationNode.Parameters, seen, typeNames);
                    break;

                case FunctionDeclarationNode functionDeclarationNode:
                    CollectTypeNamesFromFunction(functionDeclarationNode, seen, typeNames);
                    break;
            }
        }

        private static void CollectTypeNamesFromPropertyDeclaration(
            PropertyDeclarationNode propertyDeclarationNode,
            HashSet<string> seen,
            ImmutableArray<string>.Builder typeNames)
        {
            AddTypeName(propertyDeclarationNode.TypeName, seen, typeNames);
            if (propertyDeclarationNode.InitialValue is not null)
            {
                CollectTypeNamesFromValue(propertyDeclarationNode.InitialValue, seen, typeNames);
            }
        }

        private static void CollectTypeNamesFromBindingList(
            ImmutableArray<BindingNode> bindings,
            HashSet<string> seen,
            ImmutableArray<string>.Builder typeNames)
        {
            foreach (BindingNode bindingNode in bindings)
            {
                CollectTypeNamesFromValue(bindingNode.Value, seen, typeNames);
            }
        }

        private static void CollectTypeNamesFromValueList(
            ImmutableArray<BindingValue> values,
            HashSet<string> seen,
            ImmutableArray<string>.Builder typeNames)
        {
            foreach (BindingValue element in values)
            {
                CollectTypeNamesFromValue(element, seen, typeNames);
            }
        }

        private static void CollectTypeNamesFromFunction(
            FunctionDeclarationNode functionDeclarationNode,
            HashSet<string> seen,
            ImmutableArray<string>.Builder typeNames)
        {
            AddParameterTypeNames(functionDeclarationNode.Parameters, seen, typeNames);
            if (functionDeclarationNode.ReturnType is not null)
            {
                AddTypeName(functionDeclarationNode.ReturnType, seen, typeNames);
            }
        }

        private static void AddParameterTypeNames(
            ImmutableArray<ParameterDeclaration> parameters,
            HashSet<string> seen,
            ImmutableArray<string>.Builder typeNames)
        {
            foreach (ParameterDeclaration parameter in parameters)
            {
                AddTypeName(parameter.TypeName, seen, typeNames);
            }
        }

        private static void CollectTypeNamesFromValue(
            BindingValue value,
            HashSet<string> seen,
            ImmutableArray<string>.Builder typeNames)
        {
            switch (value)
            {
                case EnumReference enumReference:
                    AddTypeName(enumReference.TypeName, seen, typeNames);
                    break;

                case ObjectValue objectValue:
                    AddTypeNamesFromObject(objectValue.Object, seen, typeNames);
                    break;

                case ArrayValue arrayValue:
                    foreach (BindingValue element in arrayValue.Elements)
                    {
                        CollectTypeNamesFromValue(element, seen, typeNames);
                    }

                    break;
            }
        }

        private static void AddTypeNamesFromObject(
            ObjectDefinitionNode objectDefinitionNode,
            HashSet<string> seen,
            ImmutableArray<string>.Builder typeNames)
        {
            AddTypeName(objectDefinitionNode.TypeName, seen, typeNames);
            foreach (AstNode member in objectDefinitionNode.Members)
            {
                CollectTypeNamesFromNode(member, seen, typeNames);
            }
        }

        private static void AddTypeName(
            string typeName,
            HashSet<string> seen,
            ImmutableArray<string>.Builder typeNames)
        {
            if (seen.Add(typeName))
            {
                typeNames.Add(typeName);
            }
        }

        private static void AddLeadingComments(AstNode node, ImmutableArray<AstNode>.Builder children)
        {
            foreach (CommentNode commentNode in node.LeadingComments)
            {
                children.Add(commentNode);
            }
        }

        private static void AddStructuralChildren(AstNode node, ImmutableArray<AstNode>.Builder children)
        {
            switch (node)
            {
                case QmlDocument document:
                    children.AddRange(document.Pragmas);
                    children.AddRange(document.Imports);
                    children.Add(document.RootObject);
                    break;

                case ObjectDefinitionNode objectDefinitionNode:
                    children.AddRange(objectDefinitionNode.Members);
                    break;

                case InlineComponentNode inlineComponentNode:
                    children.Add(inlineComponentNode.Body);
                    break;

                case PropertyDeclarationNode propertyDeclarationNode when propertyDeclarationNode.InitialValue is not null:
                    AddBindingValueChildren(propertyDeclarationNode.InitialValue, children);
                    break;

                case BindingNode bindingNode:
                    AddBindingValueChildren(bindingNode.Value, children);
                    break;

                case GroupedBindingNode groupedBindingNode:
                    children.AddRange(groupedBindingNode.Bindings);
                    break;

                case AttachedBindingNode attachedBindingNode:
                    children.AddRange(attachedBindingNode.Bindings);
                    break;

                case ArrayBindingNode arrayBindingNode:
                    foreach (BindingValue value in arrayBindingNode.Elements)
                    {
                        AddBindingValueChildren(value, children);
                    }

                    break;

                case BehaviorOnNode behaviorOnNode:
                    children.Add(behaviorOnNode.Animation);
                    break;
            }
        }

        private static void AddBindingValueChildren(BindingValue value, ImmutableArray<AstNode>.Builder children)
        {
            switch (value)
            {
                case ObjectValue objectValue:
                    children.Add(objectValue.Object);
                    break;

                case ArrayValue arrayValue:
                    foreach (BindingValue element in arrayValue.Elements)
                    {
                        AddBindingValueChildren(element, children);
                    }

                    break;
            }
        }

        private static void AddTrailingComment(AstNode node, ImmutableArray<AstNode>.Builder children)
        {
            if (node.TrailingComment is not null)
            {
                children.Add(node.TrailingComment);
            }
        }

        private static int MaxObjectDepth(ObjectDefinitionNode objectDefinitionNode, int depth)
        {
            int maxDepth = depth;
            foreach (AstNode member in objectDefinitionNode.Members)
            {
                maxDepth = Math.Max(maxDepth, MaxObjectDepthFromNode(member, depth));
            }

            return maxDepth;
        }

        private static int MaxObjectDepthFromNode(AstNode node, int parentDepth)
        {
            return node switch
            {
                ObjectDefinitionNode objectDefinitionNode => MaxObjectDepth(objectDefinitionNode, parentDepth + 1),
                InlineComponentNode inlineComponentNode => MaxObjectDepth(inlineComponentNode.Body, parentDepth + 1),
                BehaviorOnNode behaviorOnNode => MaxObjectDepth(behaviorOnNode.Animation, parentDepth + 1),
                PropertyDeclarationNode propertyDeclarationNode when propertyDeclarationNode.InitialValue is not null =>
                    MaxObjectDepthFromValue(propertyDeclarationNode.InitialValue, parentDepth),
                BindingNode bindingNode => MaxObjectDepthFromValue(bindingNode.Value, parentDepth),
                GroupedBindingNode groupedBindingNode => MaxBindingNodeListDepth(groupedBindingNode.Bindings, parentDepth),
                AttachedBindingNode attachedBindingNode => MaxBindingNodeListDepth(attachedBindingNode.Bindings, parentDepth),
                ArrayBindingNode arrayBindingNode => MaxBindingValueListDepth(arrayBindingNode.Elements, parentDepth),
                _ => parentDepth,
            };
        }

        private static int MaxBindingNodeListDepth(ImmutableArray<BindingNode> bindingNodes, int parentDepth)
        {
            int maxDepth = parentDepth;
            foreach (BindingNode bindingNode in bindingNodes)
            {
                maxDepth = Math.Max(maxDepth, MaxObjectDepthFromValue(bindingNode.Value, parentDepth));
            }

            return maxDepth;
        }

        private static int MaxBindingValueListDepth(ImmutableArray<BindingValue> values, int parentDepth)
        {
            int maxDepth = parentDepth;
            foreach (BindingValue value in values)
            {
                maxDepth = Math.Max(maxDepth, MaxObjectDepthFromValue(value, parentDepth));
            }

            return maxDepth;
        }

        private static int MaxObjectDepthFromValue(BindingValue value, int parentDepth)
        {
            return value switch
            {
                ObjectValue objectValue => MaxObjectDepth(objectValue.Object, parentDepth + 1),
                ArrayValue arrayValue => MaxBindingValueListDepth(arrayValue.Elements, parentDepth),
                _ => parentDepth,
            };
        }

        private static bool NodesEqual(AstNode left, AstNode right, bool ignoreSpan)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left.GetType() != right.GetType() || left.Kind != right.Kind)
            {
                return false;
            }

            if (!ignoreSpan && !EqualityComparer<SourceSpan?>.Default.Equals(left.Span, right.Span))
            {
                return false;
            }

            if (!NodeCommentsEqual(left, right, ignoreSpan))
            {
                return false;
            }

            return left switch
            {
                QmlDocument document => DocumentEquals(document, (QmlDocument)right, ignoreSpan),
                ImportNode importNode => ImportEquals(importNode, (ImportNode)right),
                PragmaNode pragmaNode => PragmaEquals(pragmaNode, (PragmaNode)right),
                CommentNode commentNode => CommentEquals(commentNode, (CommentNode)right),
                ObjectDefinitionNode objectDefinitionNode => ObjectDefinitionEquals(objectDefinitionNode, (ObjectDefinitionNode)right, ignoreSpan),
                InlineComponentNode inlineComponentNode => InlineComponentEquals(inlineComponentNode, (InlineComponentNode)right, ignoreSpan),
                IdAssignmentNode idAssignmentNode => string.Equals(idAssignmentNode.Id, ((IdAssignmentNode)right).Id, StringComparison.Ordinal),
                PropertyDeclarationNode propertyDeclarationNode => PropertyDeclarationEquals(propertyDeclarationNode, (PropertyDeclarationNode)right, ignoreSpan),
                PropertyAliasNode propertyAliasNode => PropertyAliasEquals(propertyAliasNode, (PropertyAliasNode)right),
                BindingNode bindingNode => BindingEquals(bindingNode, (BindingNode)right, ignoreSpan),
                GroupedBindingNode groupedBindingNode => GroupedBindingEquals(groupedBindingNode, (GroupedBindingNode)right, ignoreSpan),
                AttachedBindingNode attachedBindingNode => AttachedBindingEquals(attachedBindingNode, (AttachedBindingNode)right, ignoreSpan),
                ArrayBindingNode arrayBindingNode => ArrayBindingEquals(arrayBindingNode, (ArrayBindingNode)right, ignoreSpan),
                BehaviorOnNode behaviorOnNode => BehaviorOnEquals(behaviorOnNode, (BehaviorOnNode)right, ignoreSpan),
                SignalDeclarationNode signalDeclarationNode => SignalDeclarationEquals(signalDeclarationNode, (SignalDeclarationNode)right),
                SignalHandlerNode signalHandlerNode => SignalHandlerEquals(signalHandlerNode, (SignalHandlerNode)right),
                FunctionDeclarationNode functionDeclarationNode => FunctionDeclarationEquals(functionDeclarationNode, (FunctionDeclarationNode)right),
                EnumDeclarationNode enumDeclarationNode => EnumDeclarationEquals(enumDeclarationNode, (EnumDeclarationNode)right),
                _ => false,
            };
        }

        private static bool NodeCommentsEqual(AstNode left, AstNode right, bool ignoreSpan)
        {
            return NodeArrayEqual(left.LeadingComments, right.LeadingComments, ignoreSpan)
                && NullableNodeEqual(left.TrailingComment, right.TrailingComment, ignoreSpan);
        }

        private static bool DocumentEquals(QmlDocument left, QmlDocument right, bool ignoreSpan)
        {
            return NodeArrayEqual(left.Pragmas, right.Pragmas, ignoreSpan)
                && NodeArrayEqual(left.Imports, right.Imports, ignoreSpan)
                && NodesEqual(left.RootObject, right.RootObject, ignoreSpan);
        }

        private static bool ImportEquals(ImportNode left, ImportNode right)
        {
            return left.ImportKind == right.ImportKind
                && string.Equals(left.ModuleUri, right.ModuleUri, StringComparison.Ordinal)
                && string.Equals(left.Version, right.Version, StringComparison.Ordinal)
                && string.Equals(left.Path, right.Path, StringComparison.Ordinal)
                && string.Equals(left.Qualifier, right.Qualifier, StringComparison.Ordinal);
        }

        private static bool PragmaEquals(PragmaNode left, PragmaNode right)
        {
            return left.Name == right.Name
                && string.Equals(left.Value, right.Value, StringComparison.Ordinal);
        }

        private static bool CommentEquals(CommentNode left, CommentNode right)
        {
            return string.Equals(left.Text, right.Text, StringComparison.Ordinal)
                && left.IsBlock == right.IsBlock;
        }

        private static bool ObjectDefinitionEquals(ObjectDefinitionNode left, ObjectDefinitionNode right, bool ignoreSpan)
        {
            return string.Equals(left.TypeName, right.TypeName, StringComparison.Ordinal)
                && NodeArrayEqual(left.Members, right.Members, ignoreSpan);
        }

        private static bool InlineComponentEquals(InlineComponentNode left, InlineComponentNode right, bool ignoreSpan)
        {
            return string.Equals(left.Name, right.Name, StringComparison.Ordinal)
                && NodesEqual(left.Body, right.Body, ignoreSpan);
        }

        private static bool PropertyDeclarationEquals(PropertyDeclarationNode left, PropertyDeclarationNode right, bool ignoreSpan)
        {
            return string.Equals(left.Name, right.Name, StringComparison.Ordinal)
                && string.Equals(left.TypeName, right.TypeName, StringComparison.Ordinal)
                && left.IsDefault == right.IsDefault
                && left.IsRequired == right.IsRequired
                && left.IsReadonly == right.IsReadonly
                && NullableBindingValueEqual(left.InitialValue, right.InitialValue, ignoreSpan);
        }

        private static bool PropertyAliasEquals(PropertyAliasNode left, PropertyAliasNode right)
        {
            return string.Equals(left.Name, right.Name, StringComparison.Ordinal)
                && string.Equals(left.Target, right.Target, StringComparison.Ordinal)
                && left.IsDefault == right.IsDefault;
        }

        private static bool BindingEquals(BindingNode left, BindingNode right, bool ignoreSpan)
        {
            return string.Equals(left.PropertyName, right.PropertyName, StringComparison.Ordinal)
                && BindingValueEqual(left.Value, right.Value, ignoreSpan);
        }

        private static bool GroupedBindingEquals(GroupedBindingNode left, GroupedBindingNode right, bool ignoreSpan)
        {
            return string.Equals(left.GroupName, right.GroupName, StringComparison.Ordinal)
                && NodeArrayEqual(left.Bindings, right.Bindings, ignoreSpan);
        }

        private static bool AttachedBindingEquals(AttachedBindingNode left, AttachedBindingNode right, bool ignoreSpan)
        {
            return string.Equals(left.AttachedTypeName, right.AttachedTypeName, StringComparison.Ordinal)
                && NodeArrayEqual(left.Bindings, right.Bindings, ignoreSpan);
        }

        private static bool ArrayBindingEquals(ArrayBindingNode left, ArrayBindingNode right, bool ignoreSpan)
        {
            return string.Equals(left.PropertyName, right.PropertyName, StringComparison.Ordinal)
                && BindingValueArrayEqual(left.Elements, right.Elements, ignoreSpan);
        }

        private static bool BehaviorOnEquals(BehaviorOnNode left, BehaviorOnNode right, bool ignoreSpan)
        {
            return string.Equals(left.PropertyName, right.PropertyName, StringComparison.Ordinal)
                && NodesEqual(left.Animation, right.Animation, ignoreSpan);
        }

        private static bool SignalDeclarationEquals(SignalDeclarationNode left, SignalDeclarationNode right)
        {
            return string.Equals(left.Name, right.Name, StringComparison.Ordinal)
                && ValueArrayEqual(left.Parameters, right.Parameters);
        }

        private static bool SignalHandlerEquals(SignalHandlerNode left, SignalHandlerNode right)
        {
            return string.Equals(left.HandlerName, right.HandlerName, StringComparison.Ordinal)
                && left.Form == right.Form
                && string.Equals(left.Code, right.Code, StringComparison.Ordinal)
                && NullableStringArrayEqual(left.Parameters, right.Parameters);
        }

        private static bool FunctionDeclarationEquals(FunctionDeclarationNode left, FunctionDeclarationNode right)
        {
            return string.Equals(left.Name, right.Name, StringComparison.Ordinal)
                && ValueArrayEqual(left.Parameters, right.Parameters)
                && string.Equals(left.ReturnType, right.ReturnType, StringComparison.Ordinal)
                && string.Equals(left.Body, right.Body, StringComparison.Ordinal);
        }

        private static bool EnumDeclarationEquals(EnumDeclarationNode left, EnumDeclarationNode right)
        {
            return string.Equals(left.Name, right.Name, StringComparison.Ordinal)
                && ValueArrayEqual(left.Members, right.Members);
        }

        private static bool BindingValueEqual(BindingValue left, BindingValue right, bool ignoreSpan)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left.GetType() != right.GetType() || left.Kind != right.Kind)
            {
                return false;
            }

            return left switch
            {
                NumberLiteral numberLiteral => numberLiteral.Value.Equals(((NumberLiteral)right).Value),
                StringLiteral stringLiteral => string.Equals(stringLiteral.Value, ((StringLiteral)right).Value, StringComparison.Ordinal),
                BooleanLiteral booleanLiteral => booleanLiteral.Value == ((BooleanLiteral)right).Value,
                NullLiteral => true,
                EnumReference enumReference => EnumReferenceEquals(enumReference, (EnumReference)right),
                ScriptExpression scriptExpression => string.Equals(scriptExpression.Code, ((ScriptExpression)right).Code, StringComparison.Ordinal),
                ScriptBlock scriptBlock => string.Equals(scriptBlock.Code, ((ScriptBlock)right).Code, StringComparison.Ordinal),
                ObjectValue objectValue => NodesEqual(objectValue.Object, ((ObjectValue)right).Object, ignoreSpan),
                ArrayValue arrayValue => BindingValueArrayEqual(arrayValue.Elements, ((ArrayValue)right).Elements, ignoreSpan),
                _ => false,
            };
        }

        private static bool EnumReferenceEquals(EnumReference left, EnumReference right)
        {
            return string.Equals(left.TypeName, right.TypeName, StringComparison.Ordinal)
                && string.Equals(left.MemberName, right.MemberName, StringComparison.Ordinal);
        }

        private static bool NullableNodeEqual(AstNode? left, AstNode? right, bool ignoreSpan)
        {
            if (left is null || right is null)
            {
                return left is null && right is null;
            }

            return NodesEqual(left, right, ignoreSpan);
        }

        private static bool NullableBindingValueEqual(BindingValue? left, BindingValue? right, bool ignoreSpan)
        {
            if (left is null || right is null)
            {
                return left is null && right is null;
            }

            return BindingValueEqual(left, right, ignoreSpan);
        }

        private static bool NodeArrayEqual<TNode>(ImmutableArray<TNode> left, ImmutableArray<TNode> right, bool ignoreSpan)
            where TNode : AstNode
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (int index = 0; index < left.Length; index++)
            {
                if (!NodesEqual(left[index], right[index], ignoreSpan))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool BindingValueArrayEqual(ImmutableArray<BindingValue> left, ImmutableArray<BindingValue> right, bool ignoreSpan)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (int index = 0; index < left.Length; index++)
            {
                if (!BindingValueEqual(left[index], right[index], ignoreSpan))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool NullableStringArrayEqual(ImmutableArray<string>? left, ImmutableArray<string>? right)
        {
            if (!left.HasValue || !right.HasValue)
            {
                return !left.HasValue && !right.HasValue;
            }

            return StringArrayEqual(left.GetValueOrDefault(), right.GetValueOrDefault());
        }

        private static bool StringArrayEqual(ImmutableArray<string> left, ImmutableArray<string> right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (int index = 0; index < left.Length; index++)
            {
                if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValueArrayEqual<TValue>(ImmutableArray<TValue> left, ImmutableArray<TValue> right)
            where TValue : IEquatable<TValue>
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            EqualityComparer<TValue> comparer = EqualityComparer<TValue>.Default;
            for (int index = 0; index < left.Length; index++)
            {
                if (!comparer.Equals(left[index], right[index]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

#pragma warning restore MA0048

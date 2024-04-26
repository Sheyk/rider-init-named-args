using System;
using JetBrains.Application.Progress;
using JetBrains.DocumentManagers.Transactions;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.ContextActions;
using JetBrains.ReSharper.Feature.Services.CSharp.ContextActions;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Util;
using JetBrains.ReSharper.Resources.Shell;
using JetBrains.TextControl;
using JetBrains.Util;
using System.Linq;
using System.Text;

namespace ReSharperPlugin.ContextActions;

[ContextAction(
    Group = CSharpContextActions.GroupID,
    ResourceType = typeof(Resources),
    NameResourceName = nameof(Resources.SampleContextActionName),
    DescriptionResourceName = nameof(Resources.SampleContextActionDescription),
    Priority = -10)]
public class SampleContextAction : ContextActionBase
{
    private readonly ICSharpContextActionDataProvider _provider;

    public SampleContextAction(ICSharpContextActionDataProvider provider)
    {   
        _provider = provider;
    }

    public override string Text => Resources.SampleContextActionText;

    public override bool IsAvailable(IUserDataHolder cache)
    {
        return _provider.GetSelectedElement<IObjectCreationExpression>() != null;
    }
    
    protected override Action<ITextControl> ExecutePsiTransaction(ISolution solution, IProgressIndicator progress)
    {
        var objectCreationExpression = _provider.GetSelectedElement<IObjectCreationExpression>();
        if (objectCreationExpression?.TypeReference == null) return null;

        using var command = solution.CreateTransactionCookie(DefaultAction.Commit, "Initialize named arguments");

        var typeElement = objectCreationExpression.TypeReference.Resolve().DeclaredElement as IClass;
        if (typeElement == null || typeElement.Constructors.IsEmpty()) return null;

        var constructor = typeElement.Constructors.FirstOrDefault(); // Assuming the default or first constructor
        if (constructor == null) return null;

        var factory = CSharpElementFactory.GetInstance(objectCreationExpression);
        var newArguments = new StringBuilder();
        foreach (var parameter in constructor.Parameters)
        {
            if (newArguments.Length > 0) newArguments.Append(", ");
            var languageType = parameter.Type;
            var defaultValue = languageType.IsValueType() ? $"default({languageType.GetPresentableName(_provider.PsiModule.PsiLanguage)})" : "null";
            newArguments.Append($"{parameter.ShortName}: {defaultValue}");
        }

        var newExpressionText = $"new {typeElement.ShortName}({newArguments})";
        var newExpression = factory.CreateExpression(newExpressionText);
        objectCreationExpression.ReplaceBy(newExpression);
        return null;
    }

    protected Action<ITextControl> ExecutePsiTransaction2(ISolution solution, IProgressIndicator progress)
    {
        var declaration = _provider.GetSelectedElement<IObjectCreationExpression>();
        if (declaration == null)
            return null;

        using (WriteLockCookie.Create())
        {
            var elementFactory = CSharpElementFactory.GetInstance(declaration);
            var commentToken = elementFactory.CreateComment($"// ReSharper SDK: {nameof(SampleContextAction)}");
            commentToken = ModificationUtil.AddChildBefore(declaration, commentToken);

            return x => x.Caret.MoveTo(commentToken.GetDocumentEndOffset(), CaretVisualPlacement.DontScrollIfVisible);
        }
    }
}

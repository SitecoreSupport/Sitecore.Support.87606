using Sitecore.Collections;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Data.Templates;
using Sitecore.Diagnostics;
using Sitecore.Publishing;
using Sitecore.Publishing.Pipelines.PublishItem;
using Sitecore.Sharepoint.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Sitecore.Support.Sharepoint.Data.Providers.Pipelines
{
    public class SkipUnchangedSharepointItems : PublishItemProcessor
    {
        private class EvaluationResult<T>
        {
            // Fields
            private bool hasValue;
            private T value;

            public void SetReason(string newReason)
            {
            }

            public void SetValue(T newValue)
            {
                this.value = newValue;
                this.hasValue = true;
            }

            // Properties
            public bool HasValue
            {
                get
                {
                    return this.hasValue;
                }
            }

            public T Value
            {
                get
                {
                    return this.value;
                }
            }
        }

      public override void Process(PublishItemContext context)
      {
        Assert.ArgumentNotNull(context, "context");
        if (context.Action == Sitecore.Publishing.PublishAction.PublishVersion)
        {
          if (context.VersionToPublish != null)
          {
            var isIntegration =
              context.VersionToPublish.Fields.FirstOrDefault<Field>(
                field => (field.ID == Sitecore.Sharepoint.Common.FieldIDs.IsIntegrationItem));
            if (isIntegration != null && isIntegration.Value == "1")
            {
              if (this.ShouldBeSkipped(context))
              {
                context.Action = PublishAction.None;
                context.Result = new PublishItemResult(PublishOperation.Skipped, PublishChildAction.Allow,
                  "The source and target items have the same revision number.");
              }
            }
          }
        }
      }

      private bool ShouldBeSkipped(PublishItemContext context)
        {
            var helper = context.PublishHelper;
            var sourceItem = context.VersionToPublish;
            Item targetItem = helper.GetTargetItemInLanguage(sourceItem.ID, sourceItem.Language);
            if (targetItem != null)
            {
                var isSameRevision = helper.GetType().GetMethod("IsSameRevision", BindingFlags.NonPublic | BindingFlags.Instance);            
                if (isSameRevision != null)
                {
                    object[] parameters = new object[] { sourceItem, targetItem };
                    if ((bool)isSameRevision.Invoke(helper, parameters))
                    {
                        if (this.CompareNotVersionedFields(sourceItem,targetItem))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        private bool CompareNotVersionedFields(Item sourceItem, Item targetItem)
        {
            Assert.ArgumentNotNull(sourceItem, "sourceItem");
            Assert.ArgumentNotNull(targetItem, "targetItem");
            FieldCollection fields = sourceItem.Fields;
            FieldCollection fields2 = targetItem.Fields;
            EvaluationResult<bool> result = new EvaluationResult<bool>();
            foreach (Field field in fields)
            {
                if (field.ID == Sitecore.Sharepoint.Common.FieldIDs.IsIntegrationItem)
                {
                    continue;
                }
                TemplateField definition = field.Definition;
                if (((definition != null) && !definition.IsVersioned) && ((field.GetValue(false, false) != fields2[field.ID].GetValue(false, false)) && !this.CheckFieldRelatedToClone(field.ID)))
                {
                    result.SetValue(false);
                    break;
                }
            }
            if (!result.HasValue)
            {
                if (fields.Count != fields2.Count)
                {
                    if (sourceItem.IsItemClone)
                    {
                        EvaluationResult<bool> result2 = this.CompareClonedFields(fields, fields2);
                        result.SetValue(result2.Value);
                    }
                    else
                    {
                        result.SetValue(false);
                    }
                }
                else
                {
                    result.SetValue(true);
                }
            }
            return result.Value;
        }
        private bool CheckFieldRelatedToClone(ID fieldId)
        {
            ID[] source = new ID[] { Sitecore.FieldIDs.SourceItem };
            return source.Any<ID>(fId => (fId == fieldId));
        }
        private EvaluationResult<bool> CompareClonedFields(FieldCollection fields1, FieldCollection fields2)
        {
            Assert.ArgumentNotNull(fields1, "fields1");
            Assert.ArgumentNotNull(fields2, "fields2");
            EvaluationResult<bool> result = new EvaluationResult<bool>();
            IEnumerable<ID> first = from f in fields1 select f.ID;
            IEnumerable<ID> second = from f in fields2 select f.ID;
            IEnumerable<ID> enumerable3 = first.Except<ID>(second).Union<ID>(second.Except<ID>(first));
            bool newValue = true;
            foreach (ID id in enumerable3)
            {
                if (fields1[id].Value != fields2[id].Value)
                {
                    newValue = false;
                    break;
                }
            }
            result.SetValue(newValue);
            return result;
        }
    }
}

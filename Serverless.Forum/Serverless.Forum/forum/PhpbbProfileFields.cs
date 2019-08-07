using System;
using System.Collections.Generic;

namespace Serverless.Forum.forum
{
    public partial class PhpbbProfileFields
    {
        public int FieldId { get; set; }
        public string FieldName { get; set; }
        public byte FieldType { get; set; }
        public string FieldIdent { get; set; }
        public string FieldLength { get; set; }
        public string FieldMinlen { get; set; }
        public string FieldMaxlen { get; set; }
        public string FieldNovalue { get; set; }
        public string FieldDefaultValue { get; set; }
        public string FieldValidation { get; set; }
        public byte FieldRequired { get; set; }
        public byte FieldShowOnReg { get; set; }
        public byte FieldShowOnVt { get; set; }
        public byte FieldShowProfile { get; set; }
        public byte FieldHide { get; set; }
        public byte FieldNoView { get; set; }
        public byte FieldActive { get; set; }
        public int FieldOrder { get; set; }
    }
}

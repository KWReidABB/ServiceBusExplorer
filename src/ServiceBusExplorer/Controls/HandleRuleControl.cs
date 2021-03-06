﻿#region Copyright
//=======================================================================================
// Microsoft Azure Customer Advisory Team 
//
// This sample is supplemental to the technical guidance published on my personal
// blog at http://blogs.msdn.com/b/paolos/. 
// 
// Author: Paolo Salvatori
//=======================================================================================
// Copyright (c) Microsoft Corporation. All rights reserved.
// 
// LICENSED UNDER THE APACHE LICENSE, VERSION 2.0 (THE "LICENSE"); YOU MAY NOT USE THESE 
// FILES EXCEPT IN COMPLIANCE WITH THE LICENSE. YOU MAY OBTAIN A COPY OF THE LICENSE AT 
// http://www.apache.org/licenses/LICENSE-2.0
// UNLESS REQUIRED BY APPLICABLE LAW OR AGREED TO IN WRITING, SOFTWARE DISTRIBUTED UNDER THE 
// LICENSE IS DISTRIBUTED ON AN "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY 
// KIND, EITHER EXPRESS OR IMPLIED. SEE THE LICENSE FOR THE SPECIFIC LANGUAGE GOVERNING 
// PERMISSIONS AND LIMITATIONS UNDER THE LICENSE.
//=======================================================================================
#endregion

#region Using Directives

using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Azure.ServiceBusExplorer.Forms;
using Microsoft.Azure.ServiceBusExplorer.Helpers;
using Microsoft.ServiceBus.Messaging;

#endregion

namespace Microsoft.Azure.ServiceBusExplorer.Controls
{
    public partial class HandleRuleControl : UserControl
    {
        #region DllImports
        [DllImport("user32.dll")]
        static extern bool HideCaret(IntPtr hWnd);
        #endregion

        #region Private Constants
        //***************************
        // Formats
        //***************************
        const string ExceptionFormat = "Exception: {0}";
        const string InnerExceptionFormat = "InnerException: {0}";

        //***************************
        // Texts
        //***************************
        const string RemoveText = "Remove";
        const string AddText = "Add";
        const string RuleEntity = "RuleDescription";

        //***************************
        // Messages
        //***************************
        const string NameCannotBeNull = "The Name field cannot be null.";

        //***************************
        // Tooltips
        //***************************
        const string NameTooltip = "Gets or sets the rule name.";
        const string FilterExpressionTooltip = "Gets or sets the filter expression.";
        const string FilterActionTooltip = "Gets or sets the filter action.";
        #endregion

        #region Private Fields
        readonly RuleWrapper ruleWrapper;
        readonly ServiceBusHelper serviceBusHelper;
        readonly WriteToLogDelegate writeToLog;
        bool? isFirstRule;
        #endregion

        #region Public Constructors
        public HandleRuleControl(WriteToLogDelegate writeToLog, ServiceBusHelper serviceBusHelper, RuleWrapper ruleWrapper, bool? isFirstRule)
        {
            this.writeToLog = writeToLog;
            this.serviceBusHelper = serviceBusHelper;
            this.ruleWrapper = ruleWrapper;
            this.isFirstRule = isFirstRule;
            InitializeComponent();
            InitializeData();
        } 
        #endregion

        #region Public Events
        public event Action OnCancel;
        #endregion

        #region Private Methods
        private void InitializeData()
        {
            if (ruleWrapper != null &&
                ruleWrapper.SubscriptionDescription != null &&
                ruleWrapper.RuleDescription != null)
            {
                btnCreateDelete.Text = RemoveText;
                btnCancel.Enabled = false;
                txtCreatedAt.Text = ruleWrapper.RuleDescription.CreatedAt.ToString(CultureInfo.CurrentCulture);
                txtCreatedAt.BackColor = SystemColors.Window;
                checkBoxDefault.Checked = ruleWrapper.RuleDescription.Name == RuleDescription.DefaultRuleName;
                checkBoxDefault.Enabled = false;

                SetReadOnly(this);

                if (!string.IsNullOrWhiteSpace(ruleWrapper.RuleDescription.Name))
                {
                    txtName.Text = ruleWrapper.RuleDescription.Name;
                }

                if (ruleWrapper.RuleDescription.Filter is SqlFilter sqlFilter)
                {
                    txtFilterExpression.Text = sqlFilter.SqlExpression ?? string.Empty;
                }

                if (ruleWrapper.RuleDescription.Filter is CorrelationFilter correlationFilter)
                {
                    var value = correlationFilter.ToString();
                    txtFilterExpression.Text = value.Replace("CorrelationFilter: ", string.Empty);
                }

                if (ruleWrapper.RuleDescription.Action is SqlRuleAction action)
                {
                    txtSqlFilterAction.Text = action.SqlExpression ?? string.Empty;
                }

                toolTip.SetToolTip(txtName, NameTooltip);
                toolTip.SetToolTip(txtFilterExpression, FilterExpressionTooltip);
                toolTip.SetToolTip(txtSqlFilterAction, FilterActionTooltip);
            }
            else
            {
                btnCreateDelete.Text = AddText;
                txtCreatedAt.GotFocus += textBox_GotFocus;
                if (isFirstRule.HasValue)
                {
                    checkBoxDefault.Checked = isFirstRule.Value;
                }
            }
            txtName.Focus();
        }

        static void textBox_GotFocus(object sender, EventArgs e)
        {
            if (sender is TextBox textBox)
            {
                HideCaret(textBox.Handle);
            }
        }

        void SetReadOnly(Control control)
        {
            if (control != null &&
                control.Controls.Count > 0)
            {
                for (var i = 0; i < control.Controls.Count; i++)
                {
                    if (control.Controls[i] is TextBox textBox)
                    {
                        textBox.ReadOnly = true;
                        textBox.BackColor = SystemColors.Window;
                        textBox.GotFocus += textBox_GotFocus;
                        continue;
                    }
                    SetReadOnly(control.Controls[i]);
                }
            }
        }

        void btnCreateDelete_Click(object sender, EventArgs e)
        {
            try
            {
                if (serviceBusHelper == null || ruleWrapper?.SubscriptionDescription == null)
                {
                    return;
                }

                if (btnCreateDelete.Text == RemoveText 
                    && !string.IsNullOrWhiteSpace(ruleWrapper.SubscriptionDescription?.Name) 
                    && !string.IsNullOrWhiteSpace(ruleWrapper.RuleDescription?.Name))
                {
                    using (var deleteForm = new DeleteForm(ruleWrapper.RuleDescription.Name, RuleEntity.ToLower()))
                    {
                        if (deleteForm.ShowDialog() == DialogResult.OK)
                        {
                            serviceBusHelper.RemoveRule(ruleWrapper.SubscriptionDescription, ruleWrapper.RuleDescription);
                        }
                    }
                    return;
                }

                if (btnCreateDelete.Text != AddText)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    writeToLog(NameCannotBeNull);
                    return;
                }

                var ruleDescription = new RuleDescription(txtName.Text);

                if (!string.IsNullOrWhiteSpace(txtFilterExpression.Text))
                {
                    ruleDescription.Filter = new SqlFilter(txtFilterExpression.Text);
                }
                if (!string.IsNullOrWhiteSpace(txtSqlFilterAction.Text))
                {
                    ruleDescription.Action = new SqlRuleAction(txtSqlFilterAction.Text);
                }

                ruleWrapper.RuleDescription = serviceBusHelper.AddRule(ruleWrapper.SubscriptionDescription, ruleDescription);
                InitializeData();
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        void HandleException(Exception ex)
        {
            if (string.IsNullOrWhiteSpace(ex?.Message))
            {
                return;
            }

            writeToLog(string.Format(CultureInfo.CurrentCulture, ExceptionFormat, ex.Message));

            if (!string.IsNullOrWhiteSpace(ex.InnerException?.Message))
            {
                writeToLog(string.Format(CultureInfo.CurrentCulture, InnerExceptionFormat, ex.InnerException.Message));
            }
        }

        void btnCancel_Click(object sender, EventArgs e)
        {
            OnCancel?.Invoke();
        }

        void checkBoxDefault_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxDefault.Checked)
            {
                txtName.Text = RuleDescription.DefaultRuleName;
            }
        }

        void HandleRuleControl_Resize(object sender, EventArgs e)
        {
            var width = (Size.Width - 48) / 2;
            var height = Size.Height - 152;
            grouperFilter.Size = new Size(width, height);
            grouperAction.Size = new Size(width, height);
            grouperAction.Location = new Point(grouperFilter.Location.X + width + 16, 
                                                         grouperAction.Location.Y);
            grouperName.Size = new Size(width, grouperName.Size.Height);
            grouperCreatedAt.Size = new Size(Size.Width - grouperName.Size.Width - grouperIsDefault.Size.Width - 64, 
                                             grouperCreatedAt.Size.Height);
            grouperCreatedAt.Location = new Point(grouperFilter.Location.X + width + 16,
                                                  grouperCreatedAt.Location.Y);
        }

        void button_MouseEnter(object sender, EventArgs e)
        {
            if (sender is Control control)
            {
                control.ForeColor = Color.White;
            }
        }

        void button_MouseLeave(object sender, EventArgs e)
        {
            if (sender is Control control)
            {
                control.ForeColor = SystemColors.ControlText;
            }
        }

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    components?.Dispose();
                }


                for (var i = 0; i < Controls.Count; i++)
                {
                    Controls[i].Dispose();
                }

                base.Dispose(disposing);
            }
            catch
            {
                // ignored
            }
        }
        #endregion
    }
}

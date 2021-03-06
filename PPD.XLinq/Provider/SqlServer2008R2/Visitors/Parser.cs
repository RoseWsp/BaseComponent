﻿using PPD.XLinq.TranslateModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PPD.XLinq.Provider.SqlServer2008R2.Visitors
{
    public class Parser : ParserBase
    {
        SqlExpressionParser parser = new SqlExpressionParser();
        Expression _expression;
        private string tableName;
        public override void Parse(System.Linq.Expressions.Expression expression)
        {
            _expression = expression;
            parser.ElementType = ElementType;
            parser.Parse(expression);
            BuildSql();
        }

        string GenJoinType(JoinType joinType)
        {
            if (joinType == JoinType.Inner)
            {
                return (" INNER JOIN ");
            }
            else if (joinType == JoinType.Left)
            {
                return (" Left JOIN ");
            }
            else
            {
                throw new NotSupportedException("未支持的Join类型：" + joinType);
            }
        }



        string GetTableAlias(string columnName)
        {
            if (!string.IsNullOrWhiteSpace(tableName))
            {
                return tableName;
            }
            foreach (var join in parser.Joins.Values)
            {
                if (join.Left.Name == columnName)
                {
                    return join.Left.Table.Alias;
                }
                else if (join.Right.Name == columnName)
                {
                    return join.Right.Table.Alias;
                }
                var tableInfo = TableInfoManager.GetTable(join.Left.Table.Type);
                foreach (var column in tableInfo.Columns)
                {
                    if (column.Name == columnName)
                    {
                        return join.Left.Table.Name == tableInfo.Name ? join.Left.Table.Alias : join.Right.Table.Alias;
                    }
                }
                tableInfo = TableInfoManager.GetTable(join.Right.Table.Type);
                foreach (var column in tableInfo.Columns)
                {
                    if (column.Name == columnName)
                    {
                        return join.Left.Table.Name == tableInfo.Name ? join.Left.Table.Alias : join.Right.Table.Alias;
                    }
                }
            }
            throw new Exception();
        }

        string SelectOperation(CompareType compareType)
        {
            switch (compareType)
            {
                case CompareType.And:
                    return "AND";
                case CompareType.Equal:
                    return "=";
                case CompareType.GreaterThan:
                    return ">";
                case CompareType.GreaterThanOrEqual:
                    return ">=";
                case CompareType.LessThan:
                    return "<";
                case CompareType.LessThanOrEqual:
                    return "<=";
                case CompareType.Or:
                    return "OR";
                default:
                    throw new Exception();
            }
        }


        string BuildSql(Token token)
        {
            switch (token.Type)
            {
                case TokenType.Condition:
                    return BuildCondition(token.Condition);
                case TokenType.Column:
                    return BuildSql(token.Column);
                case TokenType.Object:
                    if (token.Object == null)
                    {
                        return null;
                    }
                    var paramName = ParserUtils.GenerateAlias("param");
                    Result.Parameters.Add(paramName, token.Object);
                    return "@" + paramName;
                default:
                    throw new Exception();
            }
            throw new Exception();
        }

        string BuildCondition(Condition condition)
        {
            var left = condition.Left;
            var result = string.Empty;
            switch (condition.CompareType)
            {
                case CompareType.Not:
                    return string.Format("NOT ({0})", BuildSql(left));
                default:
                    string leftStr = BuildSql(left);
                    string rightStr = BuildSql(condition.Right);
                    if (leftStr == null)
                    {
                        return string.Format("({0} IS NULL)", rightStr);
                    }
                    else if (rightStr == null)
                    {
                        return string.Format("({0} IS NULL)", leftStr);
                    }
                    return string.Format("({0} {1} {2})", leftStr, SelectOperation(condition.CompareType), rightStr);

            }
        }

        private string BuildSql(Column column)
        {
            if (string.IsNullOrWhiteSpace(column.Table.DataBase))
            {
                var col = string.Format("[{0}].[{1}]", GetTableAlias(column.Name), column.Name);
                if (!string.IsNullOrWhiteSpace(column.Converter))
                {
                    col = string.Format(column.Converter, col);
                    var matches = Regex.Matches(col, @"@param\d+");
                    for (int i = 0; i < matches.Count; i++)
                    {
                        var match = matches[i];
                        Result.Parameters.Add(match.Value, column.ConverterParameters[i]);
                    }
                }
                return col;
            }
            else
            {
                throw new Exception();
            }
        }

        string BuildCondition(object obj)
        {
            if (obj == null)
            {
                return string.Empty;
            }
            if (obj is Token)
            {
                var result = BuildSql(obj as Token);
                return result;
            }
            else if (obj is Condition)
            {
                return BuildCondition(obj as Condition);
            }
            else if (obj is Column)
            {
                var result = BuildSql(obj as Column);
                return result;
            }
            else if (obj is bool)
            {
                var result = Convert.ToBoolean(obj);
                if (!result)
                {
                    return "1=2";
                }
                return string.Empty;
            }
            throw new Exception();
        }

        string FormatColumn(Column column, bool genColumnAlias = true)
        {
            var tblAlias = GetTableAlias(column.Table.Alias);
            string col = string.Format("[{0}].[{1}]", tblAlias, column.Name);
            if (!string.IsNullOrWhiteSpace(column.Converter))
            {
                col = string.Format(column.Converter, string.Format("[{0}].[{1}]", tblAlias, column.Name));
            }
            if (!genColumnAlias)
            {
                return col;
            }
            return string.Format("{0} [{1}]", col, column.Name);
        }

        string FormatSelectString(List<Column> columns)
        {
            if (parser.AggregationColumns.Count >= 1)
            {
                var aggrColumn = parser.AggregationColumns.FirstOrDefault();
                var columnString = string.Empty;
                if (aggrColumn.Value != null)
                {
                    columnString = FormatColumn(aggrColumn.Value, false);
                }
                switch (aggrColumn.Key)
                {
                    case "Count":
                        if (aggrColumn.Value == null)
                        {
                            return ("Count(1)");
                        }
                        return string.Format("Count({0})", columnString);
                    case "Sum":
                        return string.Format("Sum({0})", columnString);
                    case "Average":
                        return string.Format("AVG({0})", columnString);
                    default:
                        throw new Exception(aggrColumn.Key);
                }
            }
            //else if (parser.SumColumn != null)
            //{
            //    return string.Format("SUM({0})", FormatColumn(parser.SumColumn, false));
            //}
            //else if (parser.AverageColumn != null)
            //{
            //    return string.Format("AVG({0})", FormatColumn(parser.AverageColumn, false));
            //}
            else
            {
                List<string> fields = new List<string>();
                foreach (var column in columns)
                {
                    string col = FormatColumn(column);
                    fields.Add(col);
                }
                return (string.Join(",", fields));
            }

        }

        internal void BuildSql()
        {
            Result = new ParseResult();
            var columns = parser.Columns;
            var conditions = parser.Conditions;
            //var table = columns.FirstOrDefault().Table;
            var joins = parser.Joins;
            var fromBuilder = new StringBuilder("FROM ");
            var selectBuilder = new StringBuilder("SELECT ");
            if (parser.Distinct)
            {
                selectBuilder.Append(" DISTINCT ");
            }
            var whereBuilder = new StringBuilder("");
            var sqlBuilder = new StringBuilder();
            //string tableName = string.Empty;
            if (joins != null && joins.Count > 0)
            {
                var firstJoin = joins.Values.FirstOrDefault();
                var leftColumn = firstJoin.Left;
                var leftTable = leftColumn.Table;
                if (!string.IsNullOrWhiteSpace(leftTable.DataBase))
                {
                    fromBuilder.AppendFormat("[{0}].", leftTable.DataBase);
                }
                fromBuilder.AppendFormat("[{0}] ", leftTable.Name);
                fromBuilder.AppendFormat("[{0}]", leftTable.Alias);
                if (parser.NoLockTables.Contains(leftTable.Name))
                {
                    fromBuilder.Append(" WITH (NOLOCK) ");
                }
                fromBuilder.Append(GenJoinType(firstJoin.JoinType));
                var rightColumn = firstJoin.Right;
                var rightTable = rightColumn.Table;
                if (!string.IsNullOrWhiteSpace(rightTable.DataBase))
                {
                    fromBuilder.AppendFormat("[{0}].", rightTable.DataBase);
                }
                fromBuilder.AppendFormat("[{0}] ", rightTable.Name);
                //tableName = ParserUtils.GenerateAlias(rightTable.Name);
                fromBuilder.AppendFormat("[{0}]", rightTable.Alias);
                if (parser.NoLockTables.Contains(rightTable.Name))
                {
                    fromBuilder.Append(" WITH (NOLOCK) ");
                }

                fromBuilder.Append(" ON ");
                fromBuilder.AppendFormat("[{0}].[{1}]", leftTable.Alias, leftColumn.Name);
                fromBuilder.AppendFormat(" = ");
                fromBuilder.AppendFormat("[{0}].[{1}]" + Environment.NewLine, rightTable.Alias, rightColumn.Name);
                foreach (var join in joins.Values.Skip(1))
                {
                    leftColumn = join.Left;
                    leftTable = leftColumn.Table;
                    fromBuilder.Append(GenJoinType(join.JoinType));
                    rightColumn = join.Right;
                    rightTable = rightColumn.Table;
                    if (!string.IsNullOrWhiteSpace(rightTable.DataBase))
                    {
                        fromBuilder.AppendFormat("[{0}].", rightTable.DataBase);
                    }
                    fromBuilder.AppendFormat("[{0}] ", rightTable.Name);
                    //tableName = ParserUtils.GenerateAlias(rightTable.Name);
                    fromBuilder.AppendFormat("[{0}]", rightTable.Alias);
                    if (parser.NoLockTables.Contains(rightTable.Name))
                    {
                        fromBuilder.Append(" WITH (NOLOCK) ");
                    }

                    fromBuilder.Append(" ON ");
                    fromBuilder.AppendFormat("[{0}].[{1}]", leftTable.Alias, leftColumn.Name);
                    fromBuilder.AppendFormat(" = ");
                    fromBuilder.AppendFormat("[{0}].[{1}]" + Environment.NewLine, rightTable.Alias, rightColumn.Name);
                }
                selectBuilder.Append(FormatSelectString(columns));

                if (conditions.Any())
                {
                    var filters = new List<string>();
                    foreach (var condition in conditions)
                    {
                        var filter = BuildCondition(condition);
                        if (string.IsNullOrWhiteSpace(filter))
                        {
                            continue;
                        }
                        filters.Add(filter);
                    }
                    if (filters.Any())
                    {
                        whereBuilder.Append("WHERE ");
                        whereBuilder.Append(string.Join(" AND ", filters));
                    }
                }
                sqlBuilder.Clear();
                sqlBuilder.AppendFormat("{0} {1} {2}", selectBuilder.ToString(), fromBuilder.ToString(), whereBuilder.ToString());

                //sqlBuilder = new StringBuilder(); ;
                //if (needWrapSelect)
                //{
                //    sqlBuilder.AppendFormat("{0} {1}", selectBuilder.ToString(), fromBuilder.ToString());

                //    fromBuilder.Clear();
                //    tableName = ParserUtils.GenerateAlias("table");
                //    fromBuilder.AppendFormat("FROM ({0}) [{1}]", sqlBuilder.ToString(), tableName);

                //    selectBuilder.Clear();
                //    selectBuilder.Append("SELECT ");
                //    fields = new List<string>();
                //    foreach (var item in columns)
                //    {
                //        var colAlias = columnAlias[item];
                //        var col = string.Format("[{0}].[{1}] [{2}]", tableName, colAlias, ParserUtils.GenerateAlias("col"));
                //        fields.Add(col);
                //        condition = condition.Replace("[" + item.Name + "]", "[" + colAlias + "]");
                //        condition = condition.Replace("[" + item.Table.Alias + "]", "[" + tableName + "]");
                //    }
                //    selectBuilder.Append(string.Join(",", fields));

                //    var whereBuilder = new StringBuilder();
                //    if (!string.IsNullOrWhiteSpace(condition))
                //    {
                //        whereBuilder.Append("WHERE ");
                //        whereBuilder.Append(condition.Replace("@tableAlias", tableName));
                //    }

                //    sqlBuilder.Clear();
                //    sqlBuilder.AppendFormat("{0} {1} {2}", selectBuilder.ToString(), fromBuilder.ToString(), whereBuilder.ToString());
                //}
                //else
                //{
                //    var whereBuilder = new StringBuilder();
                //    if (!string.IsNullOrWhiteSpace(condition))
                //    {
                //        whereBuilder.Append("WHERE ");
                //        //foreach (var item in columns)
                //        //{
                //        //    condition = condition.Replace("#" + item.Name, item.Name);
                //        //}
                //        whereBuilder.Append(condition);
                //    }
                //    sqlBuilder.Clear();
                //    sqlBuilder.AppendFormat("{0} {1} {2}", selectBuilder.ToString(), fromBuilder.ToString(), whereBuilder.ToString());
                //}
            }
            else
            {
                fromBuilder = new StringBuilder("FROM ");
                var table = columns.FirstOrDefault().Table;
                if (!string.IsNullOrWhiteSpace(table.DataBase))
                {
                    fromBuilder.AppendFormat("[{0}].", table.DataBase);
                }
                tableName = ParserUtils.GenerateAlias(table.Name);
                fromBuilder.AppendFormat("[{0}] [{1}]", table.Name, tableName);
                if (parser.NoLockTables.Contains(table.Name))
                {
                    fromBuilder.Append(" WITH (NOLOCK) ");
                }

                selectBuilder.Append(FormatSelectString(columns));

                if (conditions.Any())
                {
                    var filters = new List<string>();
                    foreach (var condition in conditions)
                    {
                        var filter = BuildCondition(condition);
                        if (string.IsNullOrWhiteSpace(filter))
                        {
                            continue;
                        }
                        filters.Add(filter);
                    }
                    if (filters.Any())
                    {
                        whereBuilder.Append("WHERE ");
                        whereBuilder.Append(string.Join(" AND ", filters));
                    }
                }
                sqlBuilder.Clear();
                sqlBuilder.AppendFormat("{0} {1} {2}", selectBuilder.ToString(), fromBuilder.ToString(), whereBuilder.ToString());
            }
            //}
            Result.CommandText = sqlBuilder.ToString();
        }
    }
}

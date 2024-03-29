﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateFileSystem
{
    public interface ISurveyFile
    {
        double StartFootage { get; }
        double EndFootage { get; }
        List<(double footage, double value)> GetDoubleData(string fieldName, double startFootage, double endFootage);
        List<(double footage, double value)> GetDoubleData(string fieldName);
        List<(double footage, string value)> GetStringData(string fieldName);
        List<(double footage, string value)> GetStringData(string fieldName, double startFootage, double endFootage);
        Type GetDataType(string fieldName);
        List<(string fieldName, Type fieldType)> GetFields();
    }
}

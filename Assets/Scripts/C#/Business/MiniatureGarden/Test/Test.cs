using System;
using System.Collections.Generic;

public static class Test
{
    public static void HHH()
    {
        Object obj = new();
    }
}

public class Solution 
{
    public bool SearchMatrix(int[][] matrix, int target) 
    {
        for(int i = 0; i < matrix.Length; i++)
        {
            if(BinarySearch(matrix[i], target)) return true;
        }

        return false;
    }

    public bool BinarySearch(int[] nums, int target)
    {
        int left = 0, right = nums.Length, result = 0;
        int mid = 0;

        while(left <= right)
        {
            mid = (left + right) / 2;
            if(target <= nums[mid])
            {
                result = mid;
                right = mid - 1;
            }
            else left = mid + 1;
        }

        return nums[mid] == target;
    }
}
